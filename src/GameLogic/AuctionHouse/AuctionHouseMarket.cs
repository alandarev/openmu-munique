// <copyright file="AuctionHouseMarket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Threading;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The persistent market of the auction house; one per persistence context provider and game configuration, shared by
/// all game servers of the process (see <see cref="AuctionHouseMarkets"/>).
/// </summary>
/// <remarks>
/// Every operation which moves Zen or items runs in the acting player's persistence context as one database
/// transaction (<see cref="IPlayerContext.RunInTransactionAsync{T}"/>), inside the player's persistence exclusive
/// section: the affected market rows are locked, re-validated, changed together with the player's inventory and Zen,
/// and committed; on a failure the in-memory changes are restored. Another character is never touched: proceeds go into
/// the seller's collection entry. The <see cref="Index"/> of active listings is updated after each commit only.
/// </remarks>
public sealed class AuctionHouseMarket : IAuctionHouseMarket
{
    private readonly IPersistenceContextProvider _persistenceContextProvider;
    private readonly GameConfiguration _gameConfiguration;
    private readonly ILogger<AuctionHouseMarket> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly AuctionMarketReader _reader;
    private readonly AuctionSellerDelivery _delivery;
    private volatile bool _isIndexLoaded;
    private int _isExpiryRunning;
    private DateTime _nextExpiryRun;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseMarket"/> class.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider of the market's database.</param>
    /// <param name="gameConfiguration">The game configuration whose economy the market serves.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider; <see cref="TimeProvider.System"/> if <see langword="null"/>.</param>
    public AuctionHouseMarket(IPersistenceContextProvider persistenceContextProvider, GameConfiguration gameConfiguration, ILogger<AuctionHouseMarket> logger, TimeProvider? timeProvider = null)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this._gameConfiguration = gameConfiguration;
        this._logger = logger;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._reader = new AuctionMarketReader(this.Index, () => this.Now);
        this._delivery = new AuctionSellerDelivery(logger);
    }

    /// <summary>
    /// Gets the minimum time between two runs of <see cref="ExpireDueListingsAsync"/>.
    /// </summary>
    public static TimeSpan ExpiryInterval { get; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the maximum count of listings which expire in one transaction.
    /// </summary>
    public static int ExpiryBatchSize { get; } = 100;

    /// <summary>
    /// Gets the index of the active listings. It is loaded from the database at the first use of the market.
    /// </summary>
    public AuctionListingIndex Index { get; } = new();

    private DateTime Now => this._timeProvider.GetUtcNow().UtcDateTime;

    /// <inheritdoc />
    public async ValueTask<AuctionSellerSummary> GetSellerSummaryAsync(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return AuctionSellerSummary.None;
        }

        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        var characterId = character.GetId();
        var pending = await player.RunPersistenceExclusiveAsync(() => player.PersistenceContext.GetAuctionCollectionEntryIdsAsync(characterId)).ConfigureAwait(false);
        return new AuctionSellerSummary(ClampToByte(this.Index.CountOfSeller(characterId)), (ushort)Math.Min(pending.Count, ushort.MaxValue));
    }

    /// <inheritdoc />
    public async ValueTask<AuctionSearchPage> SearchAsync(Player player, AuctionSearchQuery query)
    {
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        return this._reader.Search(player, query, GetRules(player).PageSize);
    }

    /// <inheritdoc />
    public async ValueTask<AuctionGoodsPage> GetGoodsListAsync(Player player, AuctionGoodsListQuery query)
    {
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        return this._reader.GetGoodsList(player, query, GetRules(player).PageSize);
    }

    /// <inheritdoc />
    public async ValueTask<AuctionGoodsQuotation> QuoteGoodsAsync(Player player, AuctionGoodsQuoteQuery query)
    {
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        return this._reader.Quote(player, query, GetMaximumPrice(player));
    }

    /// <inheritdoc />
    public async ValueTask<AuctionBuyListingOutcome> BuyListingAsync(Player player, AuctionBuyListingOrder order)
    {
        static AuctionBuyListingOutcome Refused(AuctionHouseResult result, Guid listingId) => new(result, listingId, 0, AuctionHouseDelivery.None);

        var character = player.SelectedCharacter ?? throw new InvalidOperationException("The player has no selected character.");
        var characterId = character.GetId();
        var rules = GetRules(player);
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);

        AuctionListing? lockedListing = null;
        AuctionCollectionClaim? claim = null;
        AuctionSellerDeliveryOrder? delivery = null;
        var outcome = await RunTransactionAsync(player, async transaction =>
        {
            var listing = (await transaction.LockAsync<AuctionListing>([order.ListingId]).ConfigureAwait(false)).FirstOrDefault();
            lockedListing = listing;
            if (listing is null
                || listing.State != AuctionListingState.Active
                || listing.Kind != AuctionHouseListingKind.Item
                || listing.RemainingQuantity <= 0
                || AuctionHouseTimes.ToUtc(listing.ExpiresAt) <= this.Now)
            {
                return Refused(AuctionHouseResult.NotFound, order.ListingId);
            }

            if (listing.SellerCharacterId == characterId)
            {
                return Refused(AuctionHouseResult.OwnListing, order.ListingId);
            }

            var price = (uint)listing.UnitPrice;
            if (price != order.ReviewedPrice)
            {
                return Refused(AuctionHouseResult.QuoteChanged, order.ListingId);
            }

            var wallet = character.Inventory!;
            if (wallet.Money < price)
            {
                return Refused(AuctionHouseResult.InsufficientZen, order.ListingId);
            }

            var now = this.Now;
            Pay(transaction, wallet, price);
            var purchase = CreateEntry(player.PersistenceContext, transaction, AuctionHouseCollectionEntryKind.Purchase, characterId, listing, now, true);
            delivery = await SettleSaleAsync(player, transaction, rules, listing, 1, purchase, now).ConfigureAwait(false);
            purchase.Quantity = 1;

            claim = new AuctionCollectionClaim(player, transaction);
            claim.MarkMoneyChanged();
            var delivered = await claim.ClaimAsync(purchase).ConfigureAwait(false);
            await claim.CompleteAsync().ConfigureAwait(false);
            this._logger.LogInformation(
                "Auction listing {listing} bought by {buyer} from {seller} for {price} Zen.",
                listing.GetId(),
                character.Name,
                listing.SellerName,
                price);
            return new AuctionBuyListingOutcome(AuctionHouseResult.Success, order.ListingId, price, delivered > 0 ? AuctionHouseDelivery.Inventory : AuctionHouseDelivery.Collection);
        }).ConfigureAwait(false);

        if (lockedListing is not null)
        {
            this.Index.Apply(lockedListing);
        }

        if (claim is not null)
        {
            await claim.ShowChangesAsync().ConfigureAwait(false);
        }

        if (delivery is not null)
        {
            this.StartDeliveries([delivery]);
        }

        return outcome;
    }

    /// <inheritdoc />
    public async ValueTask<AuctionBuyGoodsOutcome> BuyGoodsAsync(Player player, AuctionBuyGoodsOrder order)
    {
        AuctionBuyGoodsOutcome Refused(AuctionHouseResult result) => new(result, order.ItemType, order.Level, 0, 0, 0, 0);

        var character = player.SelectedCharacter ?? throw new InvalidOperationException("The player has no selected character.");
        var characterId = character.GetId();
        var rules = GetRules(player);
        var maximumPrice = GetMaximumPrice(player);
        var (group, number) = AuctionHouseItemTypes.Split(order.ItemType);
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);

        var fill = AuctionGoodsFill.Create(this.Index.GetGoodsInFillOrder(group, number, order.Level, characterId, this.Now), order.Quantity);
        if (fill.Quantity < order.Quantity)
        {
            return Refused(AuctionHouseResult.InsufficientStock);
        }

        if (fill.Total > maximumPrice)
        {
            return Refused(AuctionHouseResult.PriceOutOfRange);
        }

        if (fill.Total > order.ReviewedTotal)
        {
            return Refused(AuctionHouseResult.QuoteChanged);
        }

        IReadOnlyList<AuctionListing> lockedListings = [];
        var deliveries = new List<AuctionSellerDeliveryOrder>();
        AuctionCollectionClaim? claim = null;
        var staleIndex = false;
        var outcome = await RunTransactionAsync(player, async transaction =>
        {
            var now = this.Now;
            lockedListings = await transaction.LockAsync<AuctionListing>(fill.Parts.Select(part => part.Listing.Id)).ConfigureAwait(false);
            var available = lockedListings
                .Where(listing => listing.State == AuctionListingState.Active
                                  && listing.Kind == AuctionHouseListingKind.Goods
                                  && listing.ItemGroup == group
                                  && listing.ItemNumber == number
                                  && listing.ItemLevel == order.Level
                                  && listing.SellerCharacterId != characterId
                                  && listing.RemainingQuantity > 0
                                  && AuctionHouseTimes.ToUtc(listing.ExpiresAt) > now)
                .Select(AuctionIndexedListing.Of);
            var lockedFill = AuctionGoodsFill.Create(available, order.Quantity);
            if (lockedFill.Quantity < order.Quantity)
            {
                staleIndex = true;
                return Refused(AuctionHouseResult.InsufficientStock);
            }

            if (lockedFill.Total > maximumPrice)
            {
                return Refused(AuctionHouseResult.PriceOutOfRange);
            }

            if (lockedFill.Total > order.ReviewedTotal)
            {
                return Refused(AuctionHouseResult.QuoteChanged);
            }

            var total = (uint)lockedFill.Total;
            var wallet = character.Inventory!;
            if (wallet.Money < total)
            {
                return Refused(AuctionHouseResult.InsufficientZen);
            }

            Pay(transaction, wallet, total);
            var firstListing = lockedListings.First(listing => listing.GetId() == lockedFill.Parts[0].Listing.Id);
            var purchase = CreateEntry(player.PersistenceContext, transaction, AuctionHouseCollectionEntryKind.Purchase, characterId, firstListing, now, true);
            foreach (var (part, units) in lockedFill.Parts)
            {
                var listing = lockedListings.First(l => l.GetId() == part.Id);
                deliveries.Add(await SettleSaleAsync(player, transaction, rules, listing, (int)units, purchase, now).ConfigureAwait(false));
            }

            purchase.Quantity = (int)order.Quantity;
            claim = new AuctionCollectionClaim(player, transaction);
            claim.MarkMoneyChanged();
            var delivered = await claim.ClaimAsync(purchase).ConfigureAwait(false);
            await claim.CompleteAsync().ConfigureAwait(false);
            this._logger.LogInformation(
                "{quantity} goods of type {itemType} level {level} bought by {buyer} from {listingCount} listings for {total} Zen.",
                order.Quantity,
                order.ItemType,
                order.Level,
                character.Name,
                lockedFill.Parts.Count,
                total);
            return new AuctionBuyGoodsOutcome(AuctionHouseResult.Success, order.ItemType, order.Level, order.Quantity, total, delivered, order.Quantity - delivered);
        }).ConfigureAwait(false);

        foreach (var listing in lockedListings)
        {
            this.Index.Apply(listing);
        }

        if (claim is not null)
        {
            await claim.ShowChangesAsync().ConfigureAwait(false);
        }

        if (outcome.Result == AuctionHouseResult.Success)
        {
            this.StartDeliveries(deliveries);
        }

        if (staleIndex)
        {
            // The index promised more than the locked rows hold; now it is updated, so it tells whether a fresh quote
            // can fill the quantity at another total.
            var freshFill = AuctionGoodsFill.Create(this.Index.GetGoodsInFillOrder(group, number, order.Level, characterId, this.Now), order.Quantity);
            return Refused(freshFill.Quantity < order.Quantity ? AuctionHouseResult.InsufficientStock : AuctionHouseResult.QuoteChanged);
        }

        return outcome;
    }

    /// <inheritdoc />
    public async ValueTask<AuctionSellCheckOutcome> CheckSellAsync(Player player, AuctionSellCheckQuery query)
    {
        var character = player.SelectedCharacter ?? throw new InvalidOperationException("The player has no selected character.");
        var characterId = character.GetId();
        var rules = GetRules(player);
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        var activeListings = ClampToByte(this.Index.CountOfSeller(characterId));
        var item = player.Inventory?.GetItem(query.InventorySlot);
        if (item?.Definition is not { } definition)
        {
            return new AuctionSellCheckOutcome(AuctionHouseResult.Success, query.InventorySlot, AuctionHouseSellCheckReason.NoItem, default, 0, 0, 0, 0, activeListings);
        }

        var (reason, kind) = Classify(rules, item, query.InventorySlot);
        uint available = 0;
        uint lowestUnitPrice = 0;
        if (reason == AuctionHouseSellCheckReason.Eligible)
        {
            if (kind == AuctionHouseListingKind.Goods)
            {
                available = (uint)GetGoodsUnits(player, rules, item).Count;
                lowestUnitPrice = this.Index.GetGoodsInFillOrder(definition.Group, definition.Number, item.Level, characterId, this.Now).FirstOrDefault()?.UnitPrice ?? 0;
            }
            else
            {
                available = 1;
            }
        }

        return new AuctionSellCheckOutcome(AuctionHouseResult.Success, query.InventorySlot, reason, kind, AuctionHouseItemTypes.GetItemType(definition), item.Level, available, lowestUnitPrice, activeListings);
    }

    /// <inheritdoc />
    public async ValueTask<AuctionCreateListingOutcome> CreateListingAsync(Player player, AuctionCreateListingOrder order)
    {
        static AuctionCreateListingOutcome Refused(AuctionHouseResult result) => new(result, Guid.Empty, 0, 0);

        var character = player.SelectedCharacter ?? throw new InvalidOperationException("The player has no selected character.");
        var inventory = player.Inventory ?? throw new InvalidOperationException("The player has no inventory.");
        var characterId = character.GetId();
        var rules = GetRules(player);
        var item = inventory.GetItem(order.InventorySlot);
        if (item?.Definition is not { } definition || Classify(rules, item, order.InventorySlot) is not (AuctionHouseSellCheckReason.Eligible, var kind))
        {
            return Refused(AuctionHouseResult.NotEligible);
        }

        List<Item> units;
        if (kind == AuctionHouseListingKind.Goods)
        {
            if (!AuctionHouseFees.IsOnPriceStep(order.UnitPrice, rules.GoodsPriceSignificantDigits))
            {
                return Refused(AuctionHouseResult.PriceNotOnStep);
            }

            units = GetGoodsUnits(player, rules, item);
            if (order.Quantity == 0 || units.Count < order.Quantity)
            {
                return Refused(AuctionHouseResult.InvalidRequest);
            }

            units = units.Take((int)order.Quantity).ToList();
        }
        else
        {
            if (order.Quantity != 1)
            {
                return Refused(AuctionHouseResult.InvalidRequest);
            }

            units = [item];
        }

        var state = AuctionBrokerState.Create(rules, player.GameContext.Configuration.MaximumInventoryMoney, AuctionSellerSummary.None);
        if (!AuctionHouseFees.TryGetPostingFee(state, order.Quantity, order.UnitPrice, out var postingFee))
        {
            return Refused(AuctionHouseResult.PriceOutOfRange);
        }

        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        AuctionListing? createdListing = null;
        var outcome = await RunTransactionAsync(player, async transaction =>
        {
            var context = player.PersistenceContext;
            var activeListings = await context.GetActiveAuctionListingCountAsync(characterId).ConfigureAwait(false);
            if (activeListings >= rules.MaximumActiveListings)
            {
                return Refused(AuctionHouseResult.ListingLimitReached);
            }

            var wallet = character.Inventory!;
            if (wallet.Money < postingFee)
            {
                return Refused(AuctionHouseResult.InsufficientZen);
            }

            if (units.Any(unit => inventory.GetItem(unit.ItemSlot) != unit))
            {
                // The inventory changed by a flow which doesn't hold the persistence lock of the player.
                return Refused(AuctionHouseResult.NotEligible);
            }

            var now = this.Now;
            var listing = context.CreateNew<AuctionListing>();
            transaction.OnRollback(async () => await context.DeleteAsync(listing).ConfigureAwait(false));
            transaction.DetachWhenCompleted(listing);
            listing.SellerCharacterId = characterId;
            listing.SellerName = character.Name;
            listing.Kind = kind;
            listing.ItemGroup = definition.Group;
            listing.ItemNumber = definition.Number;
            listing.ItemLevel = item.Level;
            listing.UnitPrice = order.UnitPrice;
            listing.ListedQuantity = (int)order.Quantity;
            listing.RemainingQuantity = (int)order.Quantity;
            listing.PostingFee = postingFee;
            listing.CreatedAt = now;
            listing.ExpiresAt = now.AddHours(order.DurationHours);
            listing.State = AuctionListingState.Active;
            var escrow = context.CreateNew<ItemStorage>();
            listing.Escrow = escrow;

            Pay(transaction, wallet, postingFee);
            foreach (var unit in units)
            {
                var slot = unit.ItemSlot;
                await inventory.RemoveItemAsync(unit).ConfigureAwait(false);
                escrow.Items.Add(unit);
                transaction.OnRollback(async () =>
                {
                    escrow.Items.Remove(unit);
                    await inventory.AddItemAsync(slot, unit).ConfigureAwait(false);
                });
            }

            createdListing = listing;
            this._logger.LogInformation(
                "Auction listing {listing} created by {seller}: {quantity} of type {itemType} level {level} at {unitPrice} Zen, posting fee {postingFee} Zen.",
                listing.GetId(),
                character.Name,
                order.Quantity,
                AuctionHouseItemTypes.GetItemType(definition),
                item.Level,
                order.UnitPrice,
                postingFee);
            return new AuctionCreateListingOutcome(AuctionHouseResult.Success, listing.GetId(), order.Quantity, postingFee);
        }).ConfigureAwait(false);

        if (createdListing is not null)
        {
            this.Index.Apply(createdListing);
            foreach (var unit in units)
            {
                await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(unit.ItemSlot)).ConfigureAwait(false);
            }

            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        return outcome;
    }

    /// <inheritdoc />
    public async ValueTask<AuctionCancelListingOutcome> CancelListingAsync(Player player, AuctionCancelListingOrder order)
    {
        var character = player.SelectedCharacter ?? throw new InvalidOperationException("The player has no selected character.");
        var characterId = character.GetId();
        var rules = GetRules(player);
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);

        AuctionListing? lockedListing = null;
        AuctionCollectionClaim? claim = null;
        var outcome = await RunTransactionAsync(player, async transaction =>
        {
            var listing = (await transaction.LockAsync<AuctionListing>([order.ListingId]).ConfigureAwait(false)).FirstOrDefault();
            lockedListing = listing;
            if (listing is null || listing.SellerCharacterId != characterId || listing.State != AuctionListingState.Active)
            {
                return new AuctionCancelListingOutcome(AuctionHouseResult.NotFound, order.ListingId, 0, 0, 0);
            }

            var remaining = listing.RemainingQuantity;
            listing.State = AuctionListingState.Cancelled;
            listing.RemainingQuantity = 0;
            transaction.OnRollback(() =>
            {
                listing.RemainingQuantity = remaining;
                listing.State = AuctionListingState.Active;
            });

            var entry = CreateEntry(player.PersistenceContext, transaction, AuctionHouseCollectionEntryKind.Cancelled, characterId, listing, this.Now, true);
            MoveItems(transaction, listing.Escrow!, entry.Escrow!, listing.Escrow!.Items.Count);
            entry.Quantity = remaining;
            var refund = rules.RefundPostingFeeOnCancel ? (uint)listing.PostingFee : 0;
            entry.UncollectedMoney = refund;

            claim = new AuctionCollectionClaim(player, transaction);
            var toInventory = await claim.ClaimAsync(entry).ConfigureAwait(false);
            await claim.CompleteAsync().ConfigureAwait(false);
            this._logger.LogInformation("Auction listing {listing} cancelled by {seller}, {remaining} units returned.", listing.GetId(), character.Name, remaining);
            return new AuctionCancelListingOutcome(AuctionHouseResult.Success, order.ListingId, toInventory, (uint)remaining - toInventory, refund);
        }).ConfigureAwait(false);

        if (lockedListing is not null)
        {
            this.Index.Apply(lockedListing);
        }

        if (claim is not null)
        {
            await claim.ShowChangesAsync().ConfigureAwait(false);
        }

        return outcome;
    }

    /// <inheritdoc />
    public async ValueTask<AuctionOwnListingList> GetOwnListingsAsync(Player player)
    {
        await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
        return this._reader.GetOwnListings(player);
    }

    /// <inheritdoc />
    public ValueTask<AuctionCollectionPage> GetCollectionAsync(Player player, AuctionCollectionQuery query)
        => this._reader.GetCollectionAsync(player, query, GetRules(player));

    /// <inheritdoc />
    public async ValueTask<AuctionClaimOutcome> ClaimAsync(Player player, AuctionClaimOrder order)
    {
        var character = player.SelectedCharacter ?? throw new InvalidOperationException("The player has no selected character.");
        var characterId = character.GetId();

        AuctionCollectionClaim? claim = null;
        var outcome = await RunTransactionAsync(player, async transaction =>
        {
            var allEntryIds = await player.PersistenceContext.GetAuctionCollectionEntryIdsAsync(characterId).ConfigureAwait(false);
            var entries = (await transaction.LockAsync<AuctionCollectionEntry>(order.EntryId is { } entryId ? [entryId] : allEntryIds).ConfigureAwait(false))
                .Where(entry => entry.OwnerCharacterId == characterId)
                .OrderBy(entry => entry.GetId())
                .ToList();
            if (order.EntryId is not null && entries.Count == 0)
            {
                return new AuctionClaimOutcome(AuctionHouseResult.NotFound, 0, 0, 0, (ushort)Math.Min(allEntryIds.Count, ushort.MaxValue), AuctionHouseKeptBack.None);
            }

            claim = new AuctionCollectionClaim(player, transaction);
            foreach (var entry in entries)
            {
                await claim.ClaimAsync(entry).ConfigureAwait(false);
            }

            await claim.CompleteAsync().ConfigureAwait(false);
            var remaining = Math.Max(0, allEntryIds.Count - claim.CompletedEntries);
            return new AuctionClaimOutcome(
                claim.KeptBack == AuctionHouseKeptBack.None ? AuctionHouseResult.Success : AuctionHouseResult.Partial,
                claim.CompletedEntries,
                claim.ItemsMoved,
                claim.ZenCollected,
                (ushort)Math.Min(remaining, ushort.MaxValue),
                claim.KeptBack);
        }).ConfigureAwait(false);

        if (claim is not null)
        {
            await claim.ShowChangesAsync().ConfigureAwait(false);
        }

        return outcome;
    }

    /// <summary>
    /// Registers a game context whose players use this market, so that sellers among its players receive their
    /// deliveries and notices.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    public void RegisterGameContext(IGameContext gameContext) => this._delivery.RegisterGameContext(gameContext);

    /// <summary>
    /// Waits until the deliveries to sellers which were started so far are completed.
    /// </summary>
    /// <returns>The task.</returns>
    public Task WaitForPendingDeliveriesAsync() => this._delivery.WaitForPendingDeliveriesAsync();

    /// <summary>
    /// Expires the active listings whose duration ended: their unsold units go into an "expired" collection entry of
    /// the seller, which is delivered like proceeds. Runs at most every <see cref="ExpiryInterval"/>, unless forced,
    /// and never overlaps with another run of this market.
    /// </summary>
    /// <remarks>
    /// Each batch runs as one transaction in a context of the market. Due listings are locked with
    /// <see cref="IPersistenceTransaction.LockAvailableAsync{T}"/>, so a listing which a purchase holds is skipped and
    /// expired by a later run, if it's still active then; the state is checked after locking, so a listing is expired
    /// once, also by several markets on the same database.
    /// </remarks>
    /// <param name="force">If set to <see langword="true"/>, the interval is ignored.</param>
    /// <returns>The count of expired listings.</returns>
    public async ValueTask<int> ExpireDueListingsAsync(bool force = false)
    {
        var now = this.Now;
        if ((!force && now < this._nextExpiryRun) || Interlocked.CompareExchange(ref this._isExpiryRunning, 1, 0) != 0)
        {
            return 0;
        }

        try
        {
            this._nextExpiryRun = now + ExpiryInterval;
            await this.EnsureIndexLoadedAsync().ConfigureAwait(false);
            using var context = this._persistenceContextProvider.CreateNewPlayerContext(this._gameConfiguration);
            var expiredCount = 0;
            while (true)
            {
                var dueIds = await context.GetDueAuctionListingIdsAsync(now, ExpiryBatchSize).ConfigureAwait(false);
                if (dueIds.Count == 0)
                {
                    break;
                }

                var (lockedListings, deliveries) = await ExpireAsync(context, dueIds, now).ConfigureAwait(false);
                expiredCount += deliveries.Count;

                // The batch is committed: what follows must not turn it into a failure.
                try
                {
                    foreach (var listing in lockedListings)
                    {
                        this.Index.Apply(listing);
                    }
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Error while updating the auction house index after an expiry.");
                }

                this.StartDeliveries(deliveries);
                if (dueIds.Count < ExpiryBatchSize || deliveries.Count == 0)
                {
                    // Fewer due listings than a batch, or all were locked by purchases: a later run handles the rest.
                    break;
                }
            }

            if (expiredCount > 0)
            {
                this._logger.LogInformation("{count} auction listings expired.", expiredCount);
            }

            return expiredCount;
        }
        finally
        {
            Volatile.Write(ref this._isExpiryRunning, 0);
        }
    }

    /// <summary>
    /// Loads the index from the database, if it isn't loaded yet.
    /// </summary>
    /// <returns>The task.</returns>
    public async ValueTask EnsureIndexLoadedAsync()
    {
        if (this._isIndexLoaded)
        {
            return;
        }

        await this._loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this._isIndexLoaded)
            {
                return;
            }

            using var context = this._persistenceContextProvider.CreateNewPlayerContext(this._gameConfiguration);
            var listings = await context.GetActiveAuctionListingsAsync().ConfigureAwait(false);
            this.Index.Reset(listings.Select(AuctionIndexedListing.Of));
            this._isIndexLoaded = true;
            this._logger.LogInformation("Auction house index loaded with {count} active listings.", listings.Count);
        }
        finally
        {
            this._loadLock.Release();
        }
    }

    /// <summary>
    /// Determines whether and how the item of an inventory slot can be listed.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="item">The item.</param>
    /// <param name="slot">The inventory slot of the item.</param>
    /// <returns>The reason, and for an eligible item the kind of listing.</returns>
    internal static (AuctionHouseSellCheckReason Reason, AuctionHouseListingKind Kind) Classify(AuctionHouseRules rules, Item item, byte slot)
    {
        var definition = item.Definition!;
        var isGoods = rules.IsGoods(definition);
        var kind = isGoods ? AuctionHouseListingKind.Goods : AuctionHouseListingKind.Item;
        if (!definition.IsTradable || definition.IsBoundToCharacter)
        {
            return (AuctionHouseSellCheckReason.NotTradable, kind);
        }

        if (!IsLooseSlot(slot) || item.IsStackable())
        {
            // Equipped items, items in the personal store and stackable consumables are not accepted.
            return (AuctionHouseSellCheckReason.NotAccepted, kind);
        }

        return (AuctionHouseSellCheckReason.Eligible, kind);
    }

    private static bool IsLooseSlot(byte slot)
        => slot > InventoryConstants.LastEquippableItemSlotIndex && slot < InventoryConstants.FirstStoreItemSlotIndex;

    /// <summary>
    /// Gets the identical loose units of goods, starting with the given item, then the following slots, then the
    /// preceding slots.
    /// </summary>
    private static List<Item> GetGoodsUnits(Player player, AuctionHouseRules rules, Item first)
    {
        var definition = first.Definition!;
        var others = player.Inventory!.Items
            .Where(item => item != first
                           && item.Definition is { } other
                           && other.Group == definition.Group
                           && other.Number == definition.Number
                           && item.Level == first.Level
                           && Classify(rules, item, item.ItemSlot).Reason == AuctionHouseSellCheckReason.Eligible)
            .OrderBy(item => item.ItemSlot < first.ItemSlot)
            .ThenBy(item => item.ItemSlot);
        return [first, .. others];
    }

    private static AuctionHouseRules GetRules(Player player)
        => player.GameContext.FeaturePlugIns.GetPlugIn<AuctionHouseFeaturePlugIn>()?.Configuration ?? AuctionHouseRules.Default;

    private static byte ClampToByte(int value) => (byte)Math.Min(value, byte.MaxValue);

    private static AuctionCollectionEntry CreateEntry(IPlayerContext context, IPersistenceTransaction transaction, AuctionHouseCollectionEntryKind kind, Guid ownerCharacterId, AuctionListing listing, DateTime now, bool withEscrow)
    {
        var entry = context.CreateNew<AuctionCollectionEntry>();
        transaction.OnRollback(async () => await context.DeleteAsync(entry).ConfigureAwait(false));
        transaction.DetachWhenCompleted(entry);
        entry.OwnerCharacterId = ownerCharacterId;
        entry.Kind = kind;
        entry.ListingId = listing.GetId();
        entry.ItemGroup = listing.ItemGroup;
        entry.ItemNumber = listing.ItemNumber;
        entry.ItemLevel = listing.ItemLevel;
        entry.UpdatedAt = now;
        if (withEscrow)
        {
            entry.Escrow = context.CreateNew<ItemStorage>();
        }

        return entry;
    }

    private static void MoveItems(IPersistenceTransaction transaction, ItemStorage source, ItemStorage target, int count)
    {
        foreach (var item in source.Items.Take(count).ToList())
        {
            source.Items.Remove(item);
            target.Items.Add(item);
            transaction.OnRollback(() =>
            {
                target.Items.Remove(item);
                source.Items.Add(item);
            });
        }
    }

    private static void Pay(IPersistenceTransaction transaction, ItemStorage wallet, uint amount)
    {
        wallet.Money -= (int)amount;
        transaction.OnRollback(() => wallet.Money += (int)amount);
    }

    private static async ValueTask<(IReadOnlyList<AuctionListing> Listings, List<AuctionSellerDeliveryOrder> Deliveries)> ExpireAsync(IPlayerContext context, IReadOnlyList<Guid> dueIds, DateTime now)
    {
        IReadOnlyList<AuctionListing> lockedListings = [];
        var deliveries = new List<AuctionSellerDeliveryOrder>();
        await context.RunInTransactionAsync(async transaction =>
        {
            lockedListings = await transaction.LockAvailableAsync<AuctionListing>(dueIds).ConfigureAwait(false);
            foreach (var listing in lockedListings.Where(listing => listing.State == AuctionListingState.Active && AuctionHouseTimes.ToUtc(listing.ExpiresAt) <= now))
            {
                var remaining = listing.RemainingQuantity;
                listing.State = AuctionListingState.Expired;
                listing.RemainingQuantity = 0;
                transaction.OnRollback(() =>
                {
                    listing.RemainingQuantity = remaining;
                    listing.State = AuctionListingState.Active;
                });

                var entry = CreateEntry(context, transaction, AuctionHouseCollectionEntryKind.Expired, listing.SellerCharacterId, listing, now, true);
                MoveItems(transaction, listing.Escrow!, entry.Escrow!, listing.Escrow!.Items.Count);
                entry.Quantity = remaining;
                deliveries.Add(new AuctionSellerDeliveryOrder(
                    listing.SellerCharacterId,
                    listing.SellerName,
                    entry.GetId(),
                    AuctionHouseNoticeKind.ExpiredToInventory,
                    AuctionHouseNoticeKind.Expired,
                    AuctionHouseItemTypes.GetItemType(listing.ItemGroup, listing.ItemNumber),
                    listing.ItemLevel,
                    (uint)remaining,
                    0));
            }

            return true;
        }).ConfigureAwait(false);

        return (lockedListings, deliveries);
    }

    private static uint GetMaximumPrice(Player player)
        => AuctionBrokerState.Create(GetRules(player), player.GameContext.Configuration.MaximumInventoryMoney, AuctionSellerSummary.None).MaximumPrice;

    private static ValueTask<T> RunTransactionAsync<T>(Player player, Func<IPersistenceTransaction, ValueTask<T>> operation)
        => player.RunPersistenceExclusiveAsync(() => player.PersistenceContext.RunInTransactionAsync(operation));

    /// <summary>
    /// Sells units of a locked listing into the buyer's purchase entry: moves the items, reduces the listing,
    /// accumulates the proceeds in the seller's proceeds entry and records the sale.
    /// </summary>
    /// <returns>The delivery of the proceeds to the seller, to start after the commit.</returns>
    private static async ValueTask<AuctionSellerDeliveryOrder> SettleSaleAsync(Player buyer, IPersistenceTransaction transaction, AuctionHouseRules rules, AuctionListing listing, int units, AuctionCollectionEntry purchase, DateTime now)
    {
        var context = buyer.PersistenceContext;
        MoveItems(transaction, listing.Escrow!, purchase.Escrow!, units);
        var remaining = listing.RemainingQuantity;
        var state = listing.State;
        listing.RemainingQuantity -= units;
        if (listing.RemainingQuantity <= 0)
        {
            listing.State = AuctionListingState.SoldOut;
        }

        transaction.OnRollback(() =>
        {
            listing.RemainingQuantity = remaining;
            listing.State = state;
        });

        var gross = (uint)units * (uint)listing.UnitPrice;
        var fee = AuctionHouseFees.GetSaleFee(gross, rules.SaleFeeBasisPoints);
        var listingId = listing.GetId();

        // The listing lock serializes every purchase of this listing, so there is at most one proceeds entry to find.
        var proceedsIds = await context.GetAuctionCollectionEntryIdsAsync(listing.SellerCharacterId, listingId, AuctionHouseCollectionEntryKind.Proceeds).ConfigureAwait(false);
        var proceeds = proceedsIds.Count > 0
            ? (await transaction.LockAsync<AuctionCollectionEntry>(proceedsIds).ConfigureAwait(false)).FirstOrDefault()
            : null;
        if (proceeds is null)
        {
            proceeds = CreateEntry(context, transaction, AuctionHouseCollectionEntryKind.Proceeds, listing.SellerCharacterId, listing, now, false);
        }

        var (oldQuantity, oldGross, oldFee, oldMoney, oldUpdatedAt) = (proceeds.Quantity, proceeds.Gross, proceeds.Fee, proceeds.UncollectedMoney, proceeds.UpdatedAt);
        proceeds.Quantity += units;
        proceeds.Gross += gross;
        proceeds.Fee += fee;
        proceeds.UncollectedMoney += gross - fee;
        proceeds.UpdatedAt = now;
        var proceedsEntry = proceeds;
        transaction.OnRollback(() =>
        {
            proceedsEntry.Quantity = oldQuantity;
            proceedsEntry.Gross = oldGross;
            proceedsEntry.Fee = oldFee;
            proceedsEntry.UncollectedMoney = oldMoney;
            proceedsEntry.UpdatedAt = oldUpdatedAt;
        });

        var sale = context.CreateNew<AuctionSale>();
        transaction.OnRollback(async () => await context.DeleteAsync(sale).ConfigureAwait(false));
        transaction.DetachWhenCompleted(sale);
        sale.SoldAt = now;
        sale.ListingId = listingId;
        sale.ItemGroup = listing.ItemGroup;
        sale.ItemNumber = listing.ItemNumber;
        sale.ItemLevel = listing.ItemLevel;
        sale.Quantity = units;
        sale.UnitPrice = listing.UnitPrice;
        sale.SellerCharacterId = listing.SellerCharacterId;
        sale.SellerName = listing.SellerName;
        var buyerCharacter = buyer.SelectedCharacter!;
        sale.BuyerCharacterId = buyerCharacter.GetId();
        sale.BuyerName = buyerCharacter.Name;
        sale.Gross = gross;
        sale.Fee = fee;

        return new AuctionSellerDeliveryOrder(
            listing.SellerCharacterId,
            listing.SellerName,
            proceedsEntry.GetId(),
            AuctionHouseNoticeKind.SoldToInventory,
            AuctionHouseNoticeKind.Sold,
            AuctionHouseItemTypes.GetItemType(listing.ItemGroup, listing.ItemNumber),
            listing.ItemLevel,
            (uint)units,
            gross - fee);
    }

    /// <summary>
    /// Starts the deliveries to sellers of a committed operation; a failure is logged, never thrown, because the
    /// operation is committed already.
    /// </summary>
    /// <param name="deliveries">The deliveries.</param>
    private void StartDeliveries(IEnumerable<AuctionSellerDeliveryOrder> deliveries)
    {
        foreach (var delivery in deliveries)
        {
            try
            {
                this._delivery.Start(delivery);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error while starting the delivery of the auction house collection entry {entry} to {seller}.", delivery.EntryId, delivery.CharacterName);
            }
        }
    }
}
