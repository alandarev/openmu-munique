// <copyright file="AuctionHouseAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.AuctionHouse;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;

/// <summary>
/// Action of the auction house broker: opens and closes the broker, and executes the requests of a
/// player with an open broker dialog through the <see cref="IAuctionHouseMarket"/>.
/// </summary>
/// <remarks>
/// Every request is answered: <see cref="AuctionHouseResult.NotOpen"/> without an open broker dialog,
/// <see cref="AuctionHouseResult.Busy"/> above the configured request rate, <see cref="AuctionHouseResult.Unavailable"/>
/// without a market, <see cref="AuctionHouseResult.InvalidRequest"/> for values outside of the protocol ranges;
/// otherwise the market executes it, one request of a player at a time.
/// </remarks>
public class AuctionHouseAction
{
    /// <summary>
    /// The maximum count of entries of an equipment search.
    /// </summary>
    public const int MaximumSearchEntries = 64;

    /// <summary>
    /// The maximum count of name match entries of an equipment search.
    /// </summary>
    public const int MaximumNameMatchTypes = 48;

    /// <summary>
    /// The maximum count of ancient set member entries of an equipment search.
    /// </summary>
    public const int MaximumAncientSetMembers = 16;

    /// <summary>
    /// The maximum count of item types of a goods list request.
    /// </summary>
    public const int MaximumGoodsListTypes = 64;

    private const byte MaximumItemLevel = 15;

    private const byte MaximumExcellentOptionCount = 6;

    private const byte MaximumSocketCount = 5;

    private readonly CloseStoreAction _closeStoreAction = new();

    /// <summary>
    /// Opens the broker of the auction house NPC which the player talks to, and sends its state "open".
    /// The personal store of the player is closed first: a player at a broker has no open store, so that nothing of
    /// the inventory leaves it by a store purchase while the market moves items and Zen of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask OpenBrokerAsync(Player player)
    {
        if (player.ShopStorage?.StoreOpen ?? false)
        {
            await this._closeStoreAction.CloseStoreAsync(player).ConfigureAwait(false);
        }

        AuctionBrokerSession.Of(player).Open();
        await SendOpenedStateAsync(player, GetMarket(player)).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the broker of the player, if it's open, and sends its state "closed".
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="notifyClient">If set to <c>true</c>, the closed state is sent to the game client.</param>
    /// <returns><see langword="true"/>, if the broker was open; otherwise, <see langword="false"/>.</returns>
    public async ValueTask<bool> CloseBrokerAsync(Player player, bool notifyClient = true)
    {
        if (!AuctionBrokerSession.Of(player).TryClose())
        {
            return false;
        }

        if (notifyClient)
        {
            await player.InvokeViewPlugInAsync<IShowAuctionHouseStatePlugIn>(p => p.ShowAuctionHouseClosedAsync()).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Searches the item listings.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="query">The search criteria.</param>
    public ValueTask SearchAsync(Player player, ushort requestId, AuctionSearchQuery query)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionSearchPage(result, query.Page, 0, []),
            page => player.InvokeViewPlugInAsync<IShowAuctionSearchResultPlugIn>(p => p.ShowAuctionSearchResultAsync(requestId, page)),
            (market, _) => IsValid(query)
                ? market.SearchAsync(player, query)
                : ValueTask.FromResult(new AuctionSearchPage(AuctionHouseResult.InvalidRequest, query.Page, 0, [])));
    }

    /// <summary>
    /// Gets the goods list.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="query">The criteria.</param>
    public ValueTask GetGoodsListAsync(Player player, ushort requestId, AuctionGoodsListQuery query)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionGoodsPage(result, query.Page, 0, []),
            page => player.InvokeViewPlugInAsync<IShowAuctionGoodsListPlugIn>(p => p.ShowAuctionGoodsListAsync(requestId, page)),
            (market, _) => query.ItemTypes.Count <= MaximumGoodsListTypes
                ? market.GetGoodsListAsync(player, query)
                : ValueTask.FromResult(new AuctionGoodsPage(AuctionHouseResult.InvalidRequest, query.Page, 0, [])));
    }

    /// <summary>
    /// Quotes the price of a quantity of goods.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="query">The goods identity and quantity.</param>
    public ValueTask QuoteGoodsAsync(Player player, ushort requestId, AuctionGoodsQuoteQuery query)
    {
        return this.ExecuteAsync(
            player,
            result => RefusedQuotation(result, query),
            quotation => player.InvokeViewPlugInAsync<IShowAuctionGoodsQuotePlugIn>(p => p.ShowAuctionGoodsQuoteAsync(requestId, quotation)),
            (market, _) => query.Quantity > 0
                ? market.QuoteGoodsAsync(player, query)
                : ValueTask.FromResult(RefusedQuotation(AuctionHouseResult.InvalidRequest, query)));
    }

    /// <summary>
    /// Buys an item listing.
    /// </summary>
    /// <param name="player">The buying player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="order">The order.</param>
    public ValueTask BuyListingAsync(Player player, ushort requestId, AuctionBuyListingOrder order)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionBuyListingOutcome(result, order.ListingId, 0, AuctionHouseDelivery.None),
            outcome => player.InvokeViewPlugInAsync<IShowAuctionBuyListingResultPlugIn>(p => p.ShowAuctionBuyListingResultAsync(requestId, outcome)),
            (market, _) => market.BuyListingAsync(player, order));
    }

    /// <summary>
    /// Buys a quantity of goods. When the purchase is refused because the quote changed or the stock is insufficient,
    /// a fresh quotation with the same request id follows the answer.
    /// </summary>
    /// <param name="player">The buying player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="order">The order.</param>
    public ValueTask BuyGoodsAsync(Player player, ushort requestId, AuctionBuyGoodsOrder order)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionBuyGoodsOutcome(result, order.ItemType, order.Level, 0, 0, 0, 0),
            outcome => player.InvokeViewPlugInAsync<IShowAuctionBuyGoodsResultPlugIn>(p => p.ShowAuctionBuyGoodsResultAsync(requestId, outcome)),
            (market, _) => order.Quantity > 0
                ? market.BuyGoodsAsync(player, order)
                : ValueTask.FromResult(new AuctionBuyGoodsOutcome(AuctionHouseResult.InvalidRequest, order.ItemType, order.Level, 0, 0, 0, 0)),
            async (market, outcome) =>
            {
                if (outcome.Result is AuctionHouseResult.QuoteChanged or AuctionHouseResult.InsufficientStock)
                {
                    var quotation = await market.QuoteGoodsAsync(player, new AuctionGoodsQuoteQuery(order.ItemType, order.Level, order.Quantity)).ConfigureAwait(false);
                    await player.InvokeViewPlugInAsync<IShowAuctionGoodsQuotePlugIn>(p => p.ShowAuctionGoodsQuoteAsync(requestId, quotation)).ConfigureAwait(false);
                }
            });
    }

    /// <summary>
    /// Checks whether the item of an inventory slot can be listed.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="query">The inventory slot.</param>
    public ValueTask CheckSellAsync(Player player, ushort requestId, AuctionSellCheckQuery query)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionSellCheckOutcome(result, query.InventorySlot, default, default, 0, 0, 0, 0, 0),
            outcome => player.InvokeViewPlugInAsync<IShowAuctionSellCheckResultPlugIn>(p => p.ShowAuctionSellCheckResultAsync(requestId, outcome)),
            (market, _) => market.CheckSellAsync(player, query));
    }

    /// <summary>
    /// Creates a listing. The quantity, the duration, the price range and the expected posting fee are checked
    /// against the current rules first; when the expected posting fee differs, the answer is
    /// <see cref="AuctionHouseResult.RulesChanged"/>, followed by a fresh broker state.
    /// </summary>
    /// <param name="player">The selling player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="order">The order, with the posting fee which the player expects to pay.</param>
    public ValueTask CreateListingAsync(Player player, ushort requestId, AuctionCreateListingOrder order)
    {
        static AuctionCreateListingOutcome Refused(AuctionHouseResult result) => new(result, Guid.Empty, 0, 0);

        return this.ExecuteAsync(
            player,
            Refused,
            outcome => player.InvokeViewPlugInAsync<IShowAuctionCreateListingResultPlugIn>(p => p.ShowAuctionCreateListingResultAsync(requestId, outcome)),
            (market, rules) =>
            {
                var state = AuctionBrokerState.Create(rules, player.GameContext.Configuration.MaximumInventoryMoney, AuctionSellerSummary.None);
                if (order.Quantity == 0 || !state.DurationHours.Contains(order.DurationHours))
                {
                    return ValueTask.FromResult(Refused(AuctionHouseResult.InvalidRequest));
                }

                if (!AuctionHouseFees.TryGetPostingFee(state, order.Quantity, order.UnitPrice, out var postingFee))
                {
                    return ValueTask.FromResult(Refused(AuctionHouseResult.PriceOutOfRange));
                }

                if (postingFee != order.PostingFee)
                {
                    return ValueTask.FromResult(Refused(AuctionHouseResult.RulesChanged));
                }

                return market.CreateListingAsync(player, order);
            },
            async (market, outcome) =>
            {
                if (outcome.Result == AuctionHouseResult.RulesChanged)
                {
                    await SendOpenedStateAsync(player, market).ConfigureAwait(false);
                }
            });
    }

    /// <summary>
    /// Cancels an own active listing.
    /// </summary>
    /// <param name="player">The selling player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="order">The order.</param>
    public ValueTask CancelListingAsync(Player player, ushort requestId, AuctionCancelListingOrder order)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionCancelListingOutcome(result, order.ListingId, 0, 0, 0),
            outcome => player.InvokeViewPlugInAsync<IShowAuctionCancelListingResultPlugIn>(p => p.ShowAuctionCancelListingResultAsync(requestId, outcome)),
            (market, _) => market.CancelListingAsync(player, order));
    }

    /// <summary>
    /// Gets the own listings of the player.
    /// </summary>
    /// <param name="player">The selling player.</param>
    /// <param name="requestId">The request id.</param>
    public ValueTask GetOwnListingsAsync(Player player, ushort requestId)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionOwnListingList(result, []),
            listings => player.InvokeViewPlugInAsync<IShowAuctionOwnListingsPlugIn>(p => p.ShowAuctionOwnListingsAsync(requestId, listings)),
            (market, _) => market.GetOwnListingsAsync(player));
    }

    /// <summary>
    /// Gets a page of the collection of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="query">The requested page.</param>
    public ValueTask GetCollectionAsync(Player player, ushort requestId, AuctionCollectionQuery query)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionCollectionPage(result, query.Page, 0, []),
            page => player.InvokeViewPlugInAsync<IShowAuctionCollectionPlugIn>(p => p.ShowAuctionCollectionAsync(requestId, page)),
            (market, _) => market.GetCollectionAsync(player, query));
    }

    /// <summary>
    /// Claims one entry or every entry of the collection of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="order">The order.</param>
    public ValueTask ClaimAsync(Player player, ushort requestId, AuctionClaimOrder order)
    {
        return this.ExecuteAsync(
            player,
            result => new AuctionClaimOutcome(result, 0, 0, 0, 0, AuctionHouseKeptBack.None),
            outcome => player.InvokeViewPlugInAsync<IShowAuctionClaimResultPlugIn>(p => p.ShowAuctionClaimResultAsync(requestId, outcome)),
            (market, _) => market.ClaimAsync(player, order));
    }

    private static AuctionHouseFeaturePlugIn? GetFeature(Player player) => player.GameContext.FeaturePlugIns.GetPlugIn<AuctionHouseFeaturePlugIn>();

    private static AuctionHouseRules GetRules(Player player) => GetFeature(player)?.Configuration ?? AuctionHouseRules.Default;

    private static IAuctionHouseMarket? GetMarket(Player player) => AuctionHouseMarkets.GetMarket(player.GameContext);

    private static AuctionGoodsQuotation RefusedQuotation(AuctionHouseResult result, AuctionGoodsQuoteQuery query)
        => new(result, query.ItemType, query.Level, query.Quantity, 0, 0, []);

    private static async ValueTask SendOpenedStateAsync(Player player, IAuctionHouseMarket? market)
    {
        var summary = AuctionSellerSummary.None;
        if (market is not null)
        {
            try
            {
                summary = await market.GetSellerSummaryAsync(player).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Couldn't get the auction house counts of player {player}", player);
            }
        }

        var state = AuctionBrokerState.Create(GetRules(player), player.GameContext.Configuration.MaximumInventoryMoney, summary);
        await player.InvokeViewPlugInAsync<IShowAuctionHouseStatePlugIn>(p => p.ShowAuctionHouseOpenedAsync(state)).ConfigureAwait(false);
    }

    private static bool IsValid(AuctionSearchQuery query)
    {
        return Enum.IsDefined(query.Sort)
               && Enum.IsDefined(query.ClassLine)
               && Enum.IsDefined(query.Family)
               && query.UpgradeMin <= query.UpgradeMax
               && query.UpgradeMax <= MaximumItemLevel
               && query.MinExcellentCount <= MaximumExcellentOptionCount
               && query.MinSockets <= MaximumSocketCount
               && query.NameMatchTypes.Count <= MaximumNameMatchTypes
               && query.AncientSetMembers.Count <= MaximumAncientSetMembers
               && query.NameMatchTypes.Count + query.AncientSetMembers.Count <= MaximumSearchEntries
               && query.AncientSetMembers.All(member => member.Discriminator is 1 or 2);
    }

    private async ValueTask ExecuteAsync<TResult>(
        Player player,
        Func<AuctionHouseResult, TResult> refuse,
        Func<TResult, ValueTask> answer,
        Func<IAuctionHouseMarket, AuctionHouseRules, ValueTask<TResult>> execute,
        Func<IAuctionHouseMarket, TResult, ValueTask>? afterAnswer = null)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        if (!AuctionBrokerSession.IsBrokerOpen(player))
        {
            await answer(refuse(AuctionHouseResult.NotOpen)).ConfigureAwait(false);
            return;
        }

        var session = AuctionBrokerSession.Of(player);
        if (player.PersistenceContext.IsQuarantined)
        {
            // The in-memory state of the player may contradict the database; the player is being disconnected.
            await answer(refuse(AuctionHouseResult.InternalError)).ConfigureAwait(false);
            return;
        }

        var rules = GetRules(player);
        if (!session.TryTakeRequest(rules.MaximumRequestsPerSecond))
        {
            await answer(refuse(AuctionHouseResult.Busy)).ConfigureAwait(false);
            return;
        }

        if (GetMarket(player) is not { } market)
        {
            await answer(refuse(AuctionHouseResult.Unavailable)).ConfigureAwait(false);
            return;
        }

        using var requestLock = await session.RequestLock.LockAsync().ConfigureAwait(false);
        TResult result;
        try
        {
            result = await execute(market, rules).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error while executing an auction house request of player {player}", player);
            result = refuse(AuctionHouseResult.InternalError);
        }

        await answer(result).ConfigureAwait(false);
        if (afterAnswer is not null)
        {
            try
            {
                await afterAnswer(market, result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Unexpected error after answering an auction house request of player {player}", player);
            }
        }
    }
}
