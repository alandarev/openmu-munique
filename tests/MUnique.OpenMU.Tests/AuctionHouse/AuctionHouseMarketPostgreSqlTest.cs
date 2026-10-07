// <copyright file="AuctionHouseMarketPostgreSqlTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using EfModel = MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// PostgreSQL integration tests of the <see cref="AuctionHouseMarket"/>: settlement through the entity framework
/// player contexts, competing buyers, and a failing commit. Opt-in, see <see cref="PostgreSqlTestDatabase"/>.
/// </summary>
[TestFixture]
[NonParallelizable]
public class AuctionHouseMarketPostgreSqlTest
{
    private const byte Slot = AuctionHouseMarketTestHelper.FirstInventorySlot;

    private PostgreSqlTestDatabase _database = null!;
    private GameConfiguration _configuration = null!;
    private ItemDefinition _kris = null!;
    private ItemDefinition _jewelOfBless = null!;
    private IncreasableItemOption _excellentOption = null!;
    private IncreasableItemOption _socketOption = null!;
    private ItemOfItemSet _ancientSetItem = null!;
    private IGameContext _gameContext = null!;
    private AuctionHouseMarket _market = null!;

    private ushort JewelOfBlessType => AuctionHouseItemTypes.GetItemType(this._jewelOfBless);

    /// <summary>
    /// Creates a database with a minimal game configuration and a game context with its own market.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._database = await PostgreSqlTestDatabase.CreateAsync().ConfigureAwait(false);
        using (var context = this._database.ContextProvider.CreateNewContext())
        {
            var configuration = context.CreateNew<GameConfiguration>();
            configuration.MaximumInventoryMoney = int.MaxValue;
            configuration.RecoveryInterval = int.MaxValue;
            configuration.MaximumPartySize = 5;
            configuration.ItemDropDuration = TimeSpan.FromMinutes(1);
            var map = context.CreateNew<GameMapDefinition>();
            map.Number = 0;
            map.Name = "Lorencia";
            map.TerrainData = new byte[ushort.MaxValue + 3];
            configuration.Maps.Add(map);
            configuration.CharacterClasses.Add(context.CreateNew<CharacterClass>());

            var weaponSlot = context.CreateNew<ItemSlotType>();
            configuration.ItemSlotTypes.Add(weaponSlot);
            this._kris = CreateDefinition(context, configuration, 0, 0, "Kris", 1, 2, 20);
            this._kris.ItemSlot = weaponSlot;
            this._jewelOfBless = CreateDefinition(context, configuration, 14, 13, "Jewel of Bless", 1, 1, 1);

            var optionDefinition = context.CreateNew<ItemOptionDefinition>();
            optionDefinition.Name = "Excellent";
            this._excellentOption = context.CreateNew<IncreasableItemOption>();
            this._excellentOption.Number = 1;
            optionDefinition.PossibleOptions.Add(this._excellentOption);
            this._socketOption = context.CreateNew<IncreasableItemOption>();
            this._socketOption.Number = 2;
            optionDefinition.PossibleOptions.Add(this._socketOption);
            configuration.ItemOptions.Add(optionDefinition);

            var setGroup = context.CreateNew<ItemSetGroup>();
            setGroup.Name = "Ancient set";
            this._ancientSetItem = context.CreateNew<ItemOfItemSet>();
            this._ancientSetItem.ItemDefinition = this._kris;
            this._ancientSetItem.AncientSetDiscriminator = 1;
            setGroup.Items.Add(this._ancientSetItem);
            configuration.ItemSetGroups.Add(setGroup);

            Assert.That(await context.SaveChangesAsync().ConfigureAwait(false), Is.True);
            this._configuration = configuration;
        }

        var mapInitializer = new MapInitializer(this._configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(this._configuration, this._database.ContextProvider, mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        this._market = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>());
        gameContext.FeaturePlugIns.AddPlugIn(new AuctionHouseFeaturePlugIn { Configuration = AuctionHouseRules.Default, Market = this._market }, true);
        this._gameContext = gameContext;
    }

    /// <summary>
    /// Drops the database.
    /// </summary>
    [TearDown]
    public async Task TearDownAsync()
    {
        if (this._database is not null)
        {
            await this._database.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// An excellent, ancient socket item keeps its complete state in the database through the escrow, the index load
    /// of a restarted market, the purchase and the delivery into the buyer's inventory.
    /// </summary>
    [Test]
    public async Task ItemKeepsItsStateThroughEscrowAndPurchaseAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) =>
        {
            var item = AddItem(context, inventory, this._kris, Slot, 11);
            item.SocketCount = 2;
            item.HasSkill = true;
            item.Durability = 17;
            var excellent = context.CreateNew<ItemOptionLink>();
            excellent.ItemOption = this._excellentOption;
            excellent.Level = 1;
            item.ItemOptions.Add(excellent);
            var socket = context.CreateNew<ItemOptionLink>();
            socket.ItemOption = this._socketOption;
            socket.Index = 1;
            item.ItemOptions.Add(socket);
            item.ItemSetGroups.Add(this._ancientSetItem);
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 100_000_000).ConfigureAwait(false);

        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        Assert.That(created.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(seller.Inventory!.Items, Is.Empty);
        Assert.That(await this.GetDatabaseMoneyAsync(seller).ConfigureAwait(false), Is.EqualTo(99_500_000));

        var restartedMarket = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>());
        await restartedMarket.EnsureIndexLoadedAsync().ConfigureAwait(false);
        AssertItemState(restartedMarket.Index.Get(created.ListingId)?.Item);

        // Loaded rows come back as local times; the index holds UTC.
        var expiresAt = restartedMarket.Index.Get(created.ListingId)!.ExpiresAt;
        Assert.That(expiresAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(expiresAt - DateTime.UtcNow, Is.EqualTo(TimeSpan.FromHours(48)).Within(TimeSpan.FromMinutes(1)));

        var bought = await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false);
        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(bought.Delivery, Is.EqualTo(AuctionHouseDelivery.Inventory));

        using var verification = this._database.ContextProvider.CreateNewPlayerContext(this._configuration);
        var buyerAccount = await verification.GetAccountByLoginNameAsync("buyer").ConfigureAwait(false);
        var storedItem = buyerAccount!.Characters.Single().Inventory!.Items.Single();
        AssertItemState(storedItem);
        Assert.That(buyerAccount.Characters.Single().Inventory!.Money, Is.EqualTo(90_000_000));

        await using var data = this._database.CreateDataContext();
        var listing = await data.Set<EfModel.AuctionListing>().SingleAsync().ConfigureAwait(false);
        Assert.That(listing.State, Is.EqualTo(AuctionListingState.SoldOut));
        var proceeds = await data.Set<EfModel.AuctionCollectionEntry>().SingleAsync().ConfigureAwait(false);
        Assert.That(proceeds.OwnerCharacterId, Is.EqualTo(seller.SelectedCharacter!.GetId()));
        Assert.That(proceeds.UncollectedMoney, Is.EqualTo(9_000_000));
        Assert.That(await data.Set<EfModel.AuctionSale>().CountAsync().ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == listing.EscrowId).ConfigureAwait(false), Is.Zero);
    }

    /// <summary>
    /// Two buyers buy the same listing at the same moment: exactly one purchase completes, the other is answered
    /// "not found" and isn't charged.
    /// </summary>
    [Test]
    public async Task TwoBuyersOfOneListingAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 1)).ConfigureAwait(false);
        var first = await this.CreatePlayerAsync("first", 50_000_000).ConfigureAwait(false);
        var second = await this.CreatePlayerAsync("second", 50_000_000).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);

        // A third connection holds the listing row, so that both purchases provably run at the same time.
        await using var barrier = await RowLockBarrier.HoldAsync(this._database, "AuctionListing", created.ListingId).ConfigureAwait(false);
        var buying = Task.WhenAll(
            Task.Run(async () => await this._market.BuyListingAsync(first, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false)),
            Task.Run(async () => await this._market.BuyListingAsync(second, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false)));
        await barrier.WaitForWaitersAsync(2).ConfigureAwait(false);
        await barrier.ReleaseAsync().ConfigureAwait(false);
        var outcomes = await buying.ConfigureAwait(false);

        Assert.That(outcomes.Select(outcome => outcome.Result), Is.EquivalentTo(new[] { AuctionHouseResult.Success, AuctionHouseResult.NotFound }));
        var (winner, loser) = outcomes[0].Result == AuctionHouseResult.Success ? (first, second) : (second, first);
        Assert.That(winner.Money, Is.EqualTo(40_000_000));
        Assert.That(loser.Money, Is.EqualTo(50_000_000));
        Assert.That(winner.Inventory!.Items.Count(), Is.EqualTo(1));
        Assert.That(loser.Inventory!.Items, Is.Empty);
        Assert.That(await this.GetDatabaseMoneyAsync(winner).ConfigureAwait(false), Is.EqualTo(40_000_000));
        Assert.That(await this.GetDatabaseMoneyAsync(loser).ConfigureAwait(false), Is.EqualTo(50_000_000));
        await using var data = this._database.CreateDataContext();
        Assert.That(await data.Set<EfModel.AuctionSale>().CountAsync().ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(await data.Set<EfModel.AuctionCollectionEntry>().SingleAsync().ConfigureAwait(false), Has.Property(nameof(AuctionCollectionEntry.UncollectedMoney)).EqualTo(9_000_000));
    }

    /// <summary>
    /// Two goods buyers fill from the same listings at the same moment: no unit is sold twice, every completed
    /// purchase is charged exactly its total, and a refused purchase changes nothing.
    /// </summary>
    [Test]
    public async Task TwoGoodsBuyersShareListingsAsync()
    {
        var cheapSeller = await this.CreatePlayerAsync("cheap", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 10)).ConfigureAwait(false);
        var dearSeller = await this.CreatePlayerAsync("dear", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 10)).ConfigureAwait(false);
        var first = await this.CreatePlayerAsync("first", 100_000_000).ConfigureAwait(false);
        var second = await this.CreatePlayerAsync("second", 100_000_000).ConfigureAwait(false);
        var cheap = await this._market.CreateListingAsync(cheapSeller, new AuctionCreateListingOrder(Slot, 10, 1_000_000, 48, 0)).ConfigureAwait(false);
        var dear = await this._market.CreateListingAsync(dearSeller, new AuctionCreateListingOrder(Slot, 10, 1_100_000, 48, 0)).ConfigureAwait(false);

        // A third connection holds both listing rows, so that both purchases provably run at the same time.
        await using var barrier = await RowLockBarrier.HoldAsync(this._database, "AuctionListing", cheap.ListingId, dear.ListingId).ConfigureAwait(false);
        var buying = Task.WhenAll(
            Task.Run(async () => await this._market.BuyGoodsAsync(first, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 8, 20_000_000)).ConfigureAwait(false)),
            Task.Run(async () => await this._market.BuyGoodsAsync(second, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 8, 20_000_000)).ConfigureAwait(false)));
        await barrier.WaitForWaitersAsync(2).ConfigureAwait(false);
        await barrier.ReleaseAsync().ConfigureAwait(false);
        var outcomes = await buying.ConfigureAwait(false);

        // Both buyers chose the cheap listing from the index. The fill comes from the locked rows priced up to that
        // choice: the first one takes 8 cheap units, the later one finds only 2 left at that price and is answered
        // QuoteChanged (a fresh quote follows in the action), never filled with dearer units it didn't review.
        Assert.That(outcomes.Select(outcome => outcome.Result), Is.EquivalentTo(new[] { AuctionHouseResult.Success, AuctionHouseResult.QuoteChanged }));
        Assert.That(outcomes.Single(outcome => outcome.Result == AuctionHouseResult.Success).Paid, Is.EqualTo(8_000_000));
        var buyers = new[] { first, second };
        uint soldUnits = 0;
        for (var i = 0; i < 2; i++)
        {
            var paid = outcomes[i].Result == AuctionHouseResult.Success ? outcomes[i].Paid : 0;
            var units = outcomes[i].Result == AuctionHouseResult.Success ? outcomes[i].Quantity : 0;
            soldUnits += units;
            Assert.That(buyers[i].Money, Is.EqualTo(100_000_000 - (int)paid));
            Assert.That(await this.GetDatabaseMoneyAsync(buyers[i]).ConfigureAwait(false), Is.EqualTo(100_000_000 - (int)paid));
            Assert.That(buyers[i].Inventory!.Items.Count(), Is.EqualTo((int)units));
        }

        await using var data = this._database.CreateDataContext();
        var listings = await data.Set<EfModel.AuctionListing>().ToListAsync().ConfigureAwait(false);
        Assert.That(listings.Sum(listing => listing.ListedQuantity - listing.RemainingQuantity), Is.EqualTo(soldUnits));
        Assert.That(await data.Set<EfModel.AuctionSale>().SumAsync(sale => sale.Quantity).ConfigureAwait(false), Is.EqualTo(soldUnits));
        foreach (var listing in listings)
        {
            var escrowed = await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == listing.EscrowId).ConfigureAwait(false);
            Assert.That(escrowed, Is.EqualTo(listing.RemainingQuantity));
            Assert.That(this._market.Index.Get(listing.Id)?.RemainingQuantity ?? 0, Is.EqualTo(listing.RemainingQuantity));
        }
    }

    /// <summary>
    /// A failure of the save before the commit, after all in-memory changes (the database refuses the sale record), rolls
    /// everything back: the buyer's Zen and inventory are restored, the listing is unchanged, and the next purchase
    /// succeeds.
    /// </summary>
    [Test]
    public async Task FailingCommitRestoresBuyerAndListingAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 1)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 50_000_000).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        await this.ExecuteSqlAsync("""
            CREATE FUNCTION data."RefuseAuctionSale"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected failure'; END $$;
            CREATE TRIGGER "RefuseAuctionSale" BEFORE INSERT ON data."AuctionSale" FOR EACH ROW EXECUTE FUNCTION data."RefuseAuctionSale"();
            """).ConfigureAwait(false);

        Assert.That(async () => await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false), Throws.InstanceOf<DbUpdateException>());

        Assert.That(buyer.Money, Is.EqualTo(50_000_000));
        Assert.That(buyer.Inventory!.Items, Is.Empty);
        Assert.That(await this.GetDatabaseMoneyAsync(buyer).ConfigureAwait(false), Is.EqualTo(50_000_000));
        Assert.That(this._market.Index.Get(created.ListingId)?.RemainingQuantity, Is.EqualTo(1));
        await using (var data = this._database.CreateDataContext())
        {
            var listing = await data.Set<EfModel.AuctionListing>().SingleAsync().ConfigureAwait(false);
            Assert.That(listing.State, Is.EqualTo(AuctionListingState.Active));
            Assert.That(listing.RemainingQuantity, Is.EqualTo(1));
            Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == listing.EscrowId).ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await data.Set<EfModel.AuctionCollectionEntry>().CountAsync().ConfigureAwait(false), Is.Zero);
        }

        await this.ExecuteSqlAsync("""DROP TRIGGER "RefuseAuctionSale" ON data."AuctionSale";""").ConfigureAwait(false);
        var bought = await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false);

        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(await this.GetDatabaseMoneyAsync(buyer).ConfigureAwait(false), Is.EqualTo(40_000_000));
        Assert.That(buyer.Inventory!.Items.Count(), Is.EqualTo(1));
    }

    /// <summary>
    /// Goods which don't fit stay in the buyer's collection in the database, and a later claim with room delivers
    /// them; a cancelled goods listing returns its units.
    /// </summary>
    [Test]
    public async Task CollectionAndClaimInDatabaseAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 6)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 32)).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 6, 1_000_000, 48, 0)).ConfigureAwait(false);

        var bought = await this._market.BuyGoodsAsync(buyer, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 4, 4_000_000)).ConfigureAwait(false);
        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(bought.Delivered, Is.EqualTo(0), "the inventory is full");
        Assert.That(bought.Kept, Is.EqualTo(4));

        var kris = buyer.Inventory!.Items.First();
        await buyer.Inventory.RemoveItemAsync(kris).ConfigureAwait(false);
        await buyer.PersistenceContext.DeleteAsync(kris).ConfigureAwait(false);
        await buyer.SaveProgressAsync().ConfigureAwait(false);
        var claim = await this._market.ClaimAsync(buyer, new AuctionClaimOrder(null)).ConfigureAwait(false);
        Assert.That(claim.Result, Is.EqualTo(AuctionHouseResult.Partial));
        Assert.That(claim.ItemsMoved, Is.EqualTo(2));

        var cancelled = await this._market.CancelListingAsync(seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);
        Assert.That(cancelled.ToInventory, Is.EqualTo(2));
        var sellerClaim = await this._market.ClaimAsync(seller, new AuctionClaimOrder(null)).ConfigureAwait(false);
        Assert.That(sellerClaim.ZenCollected, Is.EqualTo(3_600_000));
        Assert.That(sellerClaim.Result, Is.EqualTo(AuctionHouseResult.Success));

        await using var data = this._database.CreateDataContext();
        var entry = await data.Set<EfModel.AuctionCollectionEntry>().SingleAsync().ConfigureAwait(false);
        Assert.That(entry.OwnerCharacterId, Is.EqualTo(buyer.SelectedCharacter!.GetId()));
        Assert.That(entry.Quantity, Is.EqualTo(2));
        Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == entry.EscrowId).ConfigureAwait(false), Is.EqualTo(2));
        var buyerInventoryId = ((EfModel.ItemStorage)buyer.SelectedCharacter!.Inventory!).Id;
        Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == buyerInventoryId).ConfigureAwait(false), Is.EqualTo(33));
        var sellerInventoryId = ((EfModel.ItemStorage)seller.SelectedCharacter!.Inventory!).Id;
        Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == sellerInventoryId).ConfigureAwait(false), Is.EqualTo(2));
        Assert.That(await this.GetDatabaseMoneyAsync(seller).ConfigureAwait(false), Is.EqualTo(100_000_000 - 300_000 + 3_600_000));
    }

    /// <summary>
    /// A purchase and the expiry of the same listing overlap, the purchase first: the purchase holds the row (paused
    /// right before its commit) while the expiry runs; the expiry skips the row, the purchase completes, and a later
    /// expiry finds the listing sold.
    /// </summary>
    [Test]
    public async Task PurchaseHoldingRowBeforeExpiryWinsAsync()
    {
        var (buyer, listingId, expiryMarket) = await this.PrepareRaceAsync().ConfigureAwait(false);
        var paused = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        ((Persistence.EntityFramework.EntityFrameworkContextBase)buyer.PersistenceContext).CommitFaultInjection = async isCommitted =>
        {
            if (!isCommitted)
            {
                paused.SetResult();
                await release.Task.ConfigureAwait(false);
            }
        };

        var buying = Task.Run(async () => await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false));
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        var expiredWhileHeld = await expiryMarket.ExpireDueListingsAsync(true).ConfigureAwait(false);
        release.SetResult();
        var bought = await buying.ConfigureAwait(false);
        var expiredAfter = await expiryMarket.ExpireDueListingsAsync(true).ConfigureAwait(false);

        Assert.That(expiredWhileHeld, Is.Zero, "the expiry overlapped the purchase and skipped its row");
        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(expiredAfter, Is.Zero);
        await this.AssertRaceOutcomeAsync(buyer, sold: true).ConfigureAwait(false);
    }

    /// <summary>
    /// A purchase and the expiry of the same listing overlap, the expiry first: the expiry holds the row (paused right
    /// after locking) while the purchase waits for it; the listing expires and the purchase is answered "not found"
    /// without a charge.
    /// </summary>
    [Test]
    public async Task ExpiryHoldingRowBeforePurchaseWinsAsync()
    {
        var (buyer, listingId, expiryMarket) = await this.PrepareRaceAsync().ConfigureAwait(false);
        var paused = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        expiryMarket.AfterExpiryBatchLocked = async () =>
        {
            paused.TrySetResult();
            await release.Task.ConfigureAwait(false);
        };

        var expiring = Task.Run(async () => await expiryMarket.ExpireDueListingsAsync(true).ConfigureAwait(false));
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        var buying = Task.Run(async () => await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false));
        await RowLockBarrier.WaitForLockWaitersAsync(this._database, 1).ConfigureAwait(false);
        release.SetResult();
        var expired = await expiring.ConfigureAwait(false);
        var bought = await buying.ConfigureAwait(false);

        Assert.That(expired, Is.EqualTo(1));
        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        await this.AssertRaceOutcomeAsync(buyer, sold: false).ConfigureAwait(false);
    }

    /// <summary>
    /// Two expiry runs of different markets (processes) overlap: the first holds its batch (paused after locking) while
    /// the second runs; every due listing expires exactly once.
    /// </summary>
    [Test]
    public async Task ConcurrentExpiryRunsExpireEachListingOnceAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            var seller = await this.CreatePlayerAsync("seller" + i, 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 2)).ConfigureAwait(false);
            var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 2, 1_000_000, 48, 0)).ConfigureAwait(false);
            Assert.That(created.Result, Is.EqualTo(AuctionHouseResult.Success));
        }

        var later = new ShiftedTimeProvider(TimeSpan.FromHours(49));
        var first = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), later) { ExpiryBatchSize = 2 };
        var second = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), later);
        var paused = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        first.AfterExpiryBatchLocked = async () =>
        {
            if (paused.TrySetResult())
            {
                await release.Task.ConfigureAwait(false);
            }
        };

        var firstRun = Task.Run(async () => await first.ExpireDueListingsAsync(true).ConfigureAwait(false));
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        var secondCount = await second.ExpireDueListingsAsync(true).ConfigureAwait(false);
        release.SetResult();
        var firstCount = await firstRun.ConfigureAwait(false);

        Assert.That(secondCount, Is.EqualTo(1), "the second run overlapped the first and skipped its two held listings");
        Assert.That(firstCount, Is.EqualTo(2));
        await using var data = this._database.CreateDataContext();
        Assert.That(await data.Set<EfModel.AuctionListing>().CountAsync(listing => listing.State == AuctionListingState.Expired).ConfigureAwait(false), Is.EqualTo(3));
        var entries = await data.Set<EfModel.AuctionCollectionEntry>().ToListAsync().ConfigureAwait(false);
        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries.Sum(entry => entry.Quantity), Is.EqualTo(6));
        Assert.That(entries.Select(entry => entry.OwnerCharacterId).Distinct().Count(), Is.EqualTo(3));
    }

    /// <summary>
    /// Due listings which another transaction holds don't hide the due listings behind them: with a batch size of two
    /// and the two oldest due listings held by a third connection, the run expires the three others.
    /// </summary>
    [Test]
    public async Task HeldOldestBatchDoesntStarveLaterDueListingsAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 5)).ConfigureAwait(false);
        var ids = new List<Guid>();
        for (byte i = 0; i < 5; i++)
        {
            var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder((byte)(Slot + i), 1, 1_000_000, 48, 0)).ConfigureAwait(false);
            Assert.That(created.Result, Is.EqualTo(AuctionHouseResult.Success));
            ids.Add(created.ListingId);
        }

        var expiryMarket = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), new ShiftedTimeProvider(TimeSpan.FromHours(49))) { ExpiryBatchSize = 2 };
        await using var barrier = await RowLockBarrier.HoldAsync(this._database, "AuctionListing", ids[0], ids[1]).ConfigureAwait(false);

        var expired = await expiryMarket.ExpireDueListingsAsync(true).ConfigureAwait(false);

        Assert.That(expired, Is.EqualTo(3));
        await barrier.ReleaseAsync().ConfigureAwait(false);
        Assert.That(await expiryMarket.ExpireDueListingsAsync(true).ConfigureAwait(false), Is.EqualTo(2));
    }

    /// <summary>
    /// A goods purchase which waits for a held listing row past the listing's expiry refuses the expired units, though
    /// they weren't expired when the purchase started.
    /// </summary>
    [Test]
    public async Task GoodsPurchaseWaitingPastExpiryRefusesExpiredUnitsAsync()
    {
        var time = new SettableTimeProvider();
        var market = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), time);
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 3)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 100_000_000).ConfigureAwait(false);
        var created = await market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 3, 1_000_000, 48, 0)).ConfigureAwait(false);
        time.Advance(TimeSpan.FromHours(48) - TimeSpan.FromSeconds(1));

        await using var barrier = await RowLockBarrier.HoldAsync(this._database, "AuctionListing", created.ListingId).ConfigureAwait(false);
        var buying = Task.Run(async () => await market.BuyGoodsAsync(buyer, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 2, 2_000_000)).ConfigureAwait(false));
        await barrier.WaitForWaitersAsync(1).ConfigureAwait(false);
        time.Advance(TimeSpan.FromSeconds(2));
        await barrier.ReleaseAsync().ConfigureAwait(false);
        var bought = await buying.ConfigureAwait(false);

        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.InsufficientStock));
        Assert.That(await this.GetDatabaseMoneyAsync(buyer).ConfigureAwait(false), Is.EqualTo(100_000_000));
        await using var data = this._database.CreateDataContext();
        Assert.That((await data.Set<EfModel.AuctionListing>().SingleAsync().ConfigureAwait(false)).RemainingQuantity, Is.EqualTo(3));
    }

    /// <summary>
    /// The connection of a purchase is lost between its save and its commit (its backend is terminated from a third
    /// connection): the purchase ends in the unknown-outcome path, the player is quarantined, and the database didn't
    /// commit anything: the listing is intact and the buyer's Zen unchanged.
    /// </summary>
    [Test]
    public async Task LostConnectionBeforeCommitEndsInUnknownOutcomeAsync()
    {
        var (buyer, listingId, _) = await this.PrepareRaceAsync().ConfigureAwait(false);
        var buyerInventoryId = buyer.SelectedCharacter!.Inventory!.GetId();
        ((Persistence.EntityFramework.EntityFrameworkContextBase)buyer.PersistenceContext).CommitFaultInjection = async isCommitted =>
        {
            if (!isCommitted)
            {
                // Only pauses: the purchase is saved inside the transaction, which isn't committed yet.
                await RowLockBarrier.TerminateIdleInTransactionBackendAsync(this._database).ConfigureAwait(false);
            }
        };

        Assert.That(async () => await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false), Throws.InstanceOf<TransactionOutcomeUnknownException>());

        Assert.That(buyer.PersistenceContext.IsQuarantined, Is.True);
        Assert.That(this._market.Index.Get(listingId)?.RemainingQuantity, Is.EqualTo(1), "the index has the database's state");
        await this.AssertRaceOutcomeAsync(buyerInventoryId, sold: false).ConfigureAwait(false);
    }

    /// <summary>
    /// A save and a transaction which are queued behind a transaction whose commit fails with unknown outcome are
    /// refused, although they passed the check before the context lock.
    /// </summary>
    [Test]
    public async Task QueuedSaveAndTransactionAreRefusedAfterUnknownOutcomeAsync()
    {
        var buyer = await this.CreatePlayerAsync("buyer", 50_000_000).ConfigureAwait(false);
        var context = (Persistence.EntityFramework.EntityFrameworkContextBase)buyer.PersistenceContext;
        var paused = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        context.CommitFaultInjection = async isCommitted =>
        {
            if (isCommitted)
            {
                paused.SetResult();
                await release.Task.ConfigureAwait(false);
                throw new TimeoutException("Injected loss of the commit acknowledgement.");
            }
        };
        var wallet = buyer.SelectedCharacter!.Inventory!;

        var failing = Task.Run(async () => await context.RunInTransactionAsync<bool>(_ =>
        {
            wallet.Money -= 1_000;
            return ValueTask.FromResult(true);
        }).ConfigureAwait(false));
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        Assert.That(context.IsQuarantined, Is.False);
        var queuedSave = Task.Run(async () =>
        {
            wallet.Money = 1;
            return await context.SaveChangesAsync().ConfigureAwait(false);
        });
        var queuedTransaction = Task.Run(async () => await context.RunInTransactionAsync<bool>(_ => ValueTask.FromResult(true)).ConfigureAwait(false));
        await Task.Delay(500).ConfigureAwait(false);
        Assert.That(queuedSave.IsCompleted || queuedTransaction.IsCompleted, Is.False, "both wait for the context lock");
        release.SetResult();

        Assert.That(async () => await failing.ConfigureAwait(false), Throws.InstanceOf<TransactionOutcomeUnknownException>());
        Assert.That(await queuedSave.ConfigureAwait(false), Is.False);
        Assert.That(async () => await queuedTransaction.ConfigureAwait(false), Throws.InvalidOperationException);
        Assert.That(await this.GetDatabaseMoneyAsync(buyer).ConfigureAwait(false), Is.EqualTo(50_000_000 - 1_000), "the committed transaction, but not the queued save");
    }

    /// <summary>
    /// The commit of a purchase fails with unknown outcome. Whether the database committed (<paramref name="committed"/>)
    /// or not, no restore runs, the player is quarantined and disconnected, every later save of the player is refused,
    /// the index gets the database's state, and the next load of the buyer matches the database.
    /// </summary>
    /// <param name="committed">If set to <c>true</c>, the failure comes after the database committed; otherwise right before the commit.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task CommitWithUnknownOutcomeQuarantinesBuyerAsync(bool committed)
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 1)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 50_000_000).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        var wallet = buyer.SelectedCharacter!.Inventory!;
        ((Persistence.EntityFramework.EntityFrameworkContextBase)buyer.PersistenceContext).CommitFaultInjection = isCommitted =>
            isCommitted == committed ? throw new TimeoutException("Injected loss of the commit acknowledgement.") : ValueTask.CompletedTask;

        Assert.That(async () => await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false), Throws.InstanceOf<TransactionOutcomeUnknownException>());

        Assert.That(buyer.PersistenceContext.IsQuarantined, Is.True);

        // The wallet of the character, because the disconnection may already have deselected the character.
        Assert.That(wallet.Money, Is.EqualTo(40_000_000), "no restore runs after a commit attempt");
        var expectedMoney = committed ? 40_000_000 : 50_000_000;
        Assert.That(this._market.Index.Get(created.ListingId) is null, Is.EqualTo(committed), "the index has the database's state");

        // Whatever happens to the player in memory now, it is never saved over the database.
        wallet.Money = 1;
        Assert.That(await buyer.PersistenceContext.SaveChangesAsync().ConfigureAwait(false), Is.False);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!buyer.PlayerState.CurrentState.IsDisconnectedOrFinished() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.That(buyer.PlayerState.CurrentState.IsDisconnectedOrFinished(), Is.True);

        using var reload = this._database.ContextProvider.CreateNewPlayerContext(this._configuration);
        var reloaded = (await reload.GetAccountByLoginNameAsync("buyer").ConfigureAwait(false))!.Characters.Single();
        Assert.That(reloaded.Inventory!.Money, Is.EqualTo(expectedMoney));
        Assert.That(reloaded.Inventory.Items.Count, Is.EqualTo(committed ? 1 : 0));
        await using var data = this._database.CreateDataContext();
        var listing = await data.Set<EfModel.AuctionListing>().SingleAsync().ConfigureAwait(false);
        Assert.That(listing.State, Is.EqualTo(committed ? AuctionListingState.SoldOut : AuctionListingState.Active));
        Assert.That(await data.Set<EfModel.Item>().CountAsync().ConfigureAwait(false), Is.EqualTo(1), "the item exists exactly once");
    }

    /// <summary>
    /// A goods purchase from two listings of a seller whose proceeds entries are in the opposite order of the fill, and
    /// the seller's claim of all entries, run at the same time: both complete, without a deadlock.
    /// </summary>
    [Test]
    public async Task PurchaseAndClaimAllOfProceedsDontDeadlockAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 6)).ConfigureAwait(false);
        var earlyBuyer = await this.CreatePlayerAsync("early", 100_000_000).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 100_000_000).ConfigureAwait(false);
        await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 3, 1_100_000, 48, 0)).ConfigureAwait(false);
        Assert.That((await this._market.BuyGoodsAsync(earlyBuyer, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 1, 1_100_000)).ConfigureAwait(false)).Result, Is.EqualTo(AuctionHouseResult.Success));
        await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot + 3, 3, 1_000_000, 48, 0)).ConfigureAwait(false);
        Assert.That((await this._market.BuyGoodsAsync(earlyBuyer, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 1, 1_000_000)).ConfigureAwait(false)).Result, Is.EqualTo(AuctionHouseResult.Success));
        Guid[] proceedsIds;
        await using (var data = this._database.CreateDataContext())
        {
            proceedsIds = await data.Set<EfModel.AuctionCollectionEntry>().Where(entry => entry.Kind == AuctionHouseCollectionEntryKind.Proceeds).OrderBy(entry => entry.Id).Select(entry => entry.Id).ToArrayAsync().ConfigureAwait(false);
        }

        Assert.That(proceedsIds, Has.Length.EqualTo(2), "the dear listing's entry is older, the cheap one is filled first");
        await using var barrier = await RowLockBarrier.HoldAsync(this._database, "AuctionCollectionEntry", proceedsIds).ConfigureAwait(false);
        var buying = Task.Run(async () => await this._market.BuyGoodsAsync(buyer, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 3, 3_200_000)).ConfigureAwait(false));
        var claiming = Task.Run(async () => await this._market.ClaimAsync(seller, new AuctionClaimOrder(null)).ConfigureAwait(false));
        await barrier.WaitForWaitersAsync(2).ConfigureAwait(false);
        await barrier.ReleaseAsync().ConfigureAwait(false);
        var bought = await buying.ConfigureAwait(false);
        var claimed = await claiming.ConfigureAwait(false);

        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(bought.Paid, Is.EqualTo(3_100_000));
        Assert.That(claimed.Result, Is.EqualTo(AuctionHouseResult.Success));
        await using var check = this._database.CreateDataContext();
        var proceeds = await check.Set<EfModel.AuctionCollectionEntry>().Where(entry => entry.Kind == AuctionHouseCollectionEntryKind.Proceeds).ToListAsync().ConfigureAwait(false);
        var collected = await this.GetDatabaseMoneyAsync(seller).ConfigureAwait(false) - (100_000_000 - 165_000 - 150_000);
        Assert.That(collected + proceeds.Sum(entry => entry.UncollectedMoney), Is.EqualTo(4_680_000), "every proceed is either collected or waiting, once");
    }

    /// <summary>
    /// The migration's Down deletes the escrow storages and their items before it drops the market tables.
    /// </summary>
    [Test]
    public async Task MigrationDownDeletesEscrowAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._jewelOfBless, 3)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 32)).ConfigureAwait(false);
        await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 2, 1_000_000, 48, 0)).ConfigureAwait(false);
        await this._market.BuyGoodsAsync(buyer, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 1, 1_000_000)).ConfigureAwait(false);
        await using (var data = this._database.CreateDataContext())
        {
            Assert.That(await data.Set<EfModel.AuctionCollectionEntry>().CountAsync(entry => entry.EscrowId != null).ConfigureAwait(false), Is.EqualTo(1), "the buyer's purchase waits with its item");
            await data.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20260926142218_AddItemRuleFlags").ConfigureAwait(false);
        }

        await using var connection = new Npgsql.NpgsqlConnection(this._database.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT (SELECT count(*) FROM data."Item"),
                   (SELECT count(*) FROM data."ItemStorage" s WHERE NOT EXISTS (SELECT 1 FROM data."Character" c WHERE c."InventoryId" = s."Id")
                                                            AND NOT EXISTS (SELECT 1 FROM data."Account" a WHERE a."VaultId" = s."Id"))
            """, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        Assert.That(await reader.ReadAsync().ConfigureAwait(false), Is.True);
        Assert.That(reader.GetInt64(0), Is.EqualTo(1 + 32), "only the seller's unlisted jewel and the buyer's weapons remain");
        Assert.That(reader.GetInt64(1), Is.Zero, "no orphan storage remains");
    }

    private async ValueTask<(Player Buyer, Guid ListingId, AuctionHouseMarket ExpiryMarket)> PrepareRaceAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 1)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 50_000_000).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        Assert.That(created.Result, Is.EqualTo(AuctionHouseResult.Success));
        var expiryMarket = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), new ShiftedTimeProvider(TimeSpan.FromHours(49)));
        await expiryMarket.EnsureIndexLoadedAsync().ConfigureAwait(false);
        return (buyer, created.ListingId, expiryMarket);
    }

    private async ValueTask AssertRaceOutcomeAsync(Player buyer, bool sold)
        => await this.AssertRaceOutcomeAsync(buyer.SelectedCharacter!.Inventory!.GetId(), sold).ConfigureAwait(false);

    private async ValueTask AssertRaceOutcomeAsync(Guid buyerInventoryId, bool sold)
    {
        await using var data = this._database.CreateDataContext();
        var listing = await data.Set<EfModel.AuctionListing>().SingleAsync().ConfigureAwait(false);
        var entries = await data.Set<EfModel.AuctionCollectionEntry>().ToListAsync().ConfigureAwait(false);
        if (sold)
        {
            Assert.That(listing.State, Is.EqualTo(AuctionListingState.SoldOut));
            Assert.That(await this.GetDatabaseMoneyAsync(buyerInventoryId).ConfigureAwait(false), Is.EqualTo(40_000_000));
            Assert.That(entries.Select(entry => entry.Kind), Is.EqualTo(new[] { AuctionHouseCollectionEntryKind.Proceeds }));
        }
        else
        {
            Assert.That(listing.State, Is.AnyOf(AuctionListingState.Expired, AuctionListingState.Active));
            Assert.That(await this.GetDatabaseMoneyAsync(buyerInventoryId).ConfigureAwait(false), Is.EqualTo(50_000_000));
            Assert.That(await data.Set<EfModel.AuctionSale>().CountAsync().ConfigureAwait(false), Is.Zero);
            if (listing.State == AuctionListingState.Active)
            {
                Assert.That(entries, Is.Empty);
                Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == listing.EscrowId).ConfigureAwait(false), Is.EqualTo(1));
            }
            else
            {
                Assert.That(entries.Select(entry => entry.Kind), Is.EqualTo(new[] { AuctionHouseCollectionEntryKind.Expired }));
            }
        }

        Assert.That(await data.Set<EfModel.Item>().CountAsync().ConfigureAwait(false), Is.EqualTo(1), "the item exists exactly once");
    }

    private static void AssertItemState(Item? item)
    {
        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Level, Is.EqualTo(11));
        Assert.That(item.Durability, Is.EqualTo(17));
        Assert.That(item.SocketCount, Is.EqualTo(2));
        Assert.That(item.HasSkill, Is.True);
        Assert.That(item.Definition?.Name.ToString(), Is.EqualTo("Kris"));
        Assert.That(item.ItemOptions.Select(link => (link.ItemOption?.Number, link.Level, link.Index)), Is.EquivalentTo(new (int?, int, int)[] { (1, 1, 0), (2, 0, 1) }));
        Assert.That(item.ItemSetGroups.Single().AncientSetDiscriminator, Is.EqualTo(1));
    }

    private static ItemDefinition CreateDefinition(IContext context, GameConfiguration configuration, byte group, short number, string name, byte width, byte height, byte durability)
    {
        var definition = context.CreateNew<ItemDefinition>();
        definition.Group = group;
        definition.Number = number;
        definition.Name = name;
        definition.Width = width;
        definition.Height = height;
        definition.Durability = durability;
        configuration.Items.Add(definition);
        return definition;
    }

    private static Item AddItem(IContext context, ItemStorage inventory, ItemDefinition definition, byte slot, byte level = 0)
    {
        var item = context.CreateNew<Item>();
        item.Definition = definition;
        item.Level = level;
        item.Durability = definition.Durability;
        item.ItemSlot = slot;
        inventory.Items.Add(item);
        return item;
    }

    private static ValueTask AddItemsAsync(IContext context, ItemStorage inventory, ItemDefinition definition, int count)
    {
        for (var i = 0; i < count; i++)
        {
            // 1x1 items fill row by row; 1x2 items every second row.
            var row = definition.Height == 1 ? i / 8 : i / 8 * 2;
            AddItem(context, inventory, definition, (byte)(Slot + (row * 8) + (i % 8)));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates an account with one character in the database, lets the given action add items to its inventory, and
    /// creates a player whose character is the loaded character of the account.
    /// </summary>
    private async ValueTask<Player> CreatePlayerAsync(string name, int money, Func<IContext, ItemStorage, ValueTask>? addItems = null)
    {
        using (var context = this._database.ContextProvider.CreateNewContext())
        {
            context.Attach(this._configuration);
            var account = context.CreateNew<Account>();
            account.LoginName = name;
            var character = context.CreateNew<Character>();
            character.Name = name;
            character.CharacterClass = this._configuration.CharacterClasses.Single();
            character.Inventory = context.CreateNew<ItemStorage>();
            character.Inventory.Money = money;
            account.Characters.Add(character);
            if (addItems is not null)
            {
                await addItems(context, character.Inventory).ConfigureAwait(false);
            }

            Assert.That(await context.SaveChangesAsync().ConfigureAwait(false), Is.True);
        }

        return await PlayerTestHelper.CreatePlayerAsync(this._gameContext, async playerContext =>
        {
            var account = await playerContext.GetAccountByLoginNameAsync(name).ConfigureAwait(false);
            var stored = account!.Characters.Single();
            return (Character?)new AuctionTestCharacter { Id = stored.GetId(), Name = stored.Name, Inventory = stored.Inventory };
        }).ConfigureAwait(false);
    }

    private ValueTask<long> GetDatabaseMoneyAsync(Player player) => this.GetDatabaseMoneyAsync(player.SelectedCharacter!.Inventory!.GetId());

    private async ValueTask<long> GetDatabaseMoneyAsync(Guid inventoryId)
    {
        await using var data = this._database.CreateDataContext();
        return await data.Set<EfModel.ItemStorage>().Where(storage => storage.Id == inventoryId).Select(storage => storage.Money).SingleAsync().ConfigureAwait(false);
    }

    private async ValueTask ExecuteSqlAsync(string sql)
    {
        await using var data = this._database.CreateDataContext();
        await data.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
    }

    /// <summary>
    /// A time provider which is ahead of the system time by a fixed span.
    /// </summary>
    private sealed class ShiftedTimeProvider(TimeSpan shift) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow() + shift;
    }

    /// <summary>
    /// A time provider whose time is advanced by the test, from the system time.
    /// </summary>
    private sealed class SettableTimeProvider : TimeProvider
    {
        private long _offsetTicks;

        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref this._offsetTicks));

        public void Advance(TimeSpan duration) => Interlocked.Add(ref this._offsetTicks, duration.Ticks);
    }
}
