// <copyright file="IAuctionHouseMarket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The market behind the auction house broker: it keeps the listings and the collections,
/// and executes the requests of the players.
/// </summary>
/// <remarks>
/// The auction house action checks the open broker dialog, the request rate and the
/// rule-only conditions (see <see cref="AuctionCreateListingOrder"/>), calls the market one
/// request of a player at a time, and sends the answers to the game client.
/// The market never sends packets. Results of lists may contain at most 255 rows.
/// </remarks>
public interface IAuctionHouseMarket
{
    /// <summary>
    /// Gets the counts of a player which the broker announces when it opens.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The counts of the player.</returns>
    ValueTask<AuctionSellerSummary> GetSellerSummaryAsync(Player player);

    /// <summary>
    /// Searches the item listings, without goods.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The search criteria.</param>
    /// <returns>The requested page of matching listings.</returns>
    ValueTask<AuctionSearchPage> SearchAsync(Player player, AuctionSearchQuery query);

    /// <summary>
    /// Gets the goods list, without the own listings of the player.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The criteria.</param>
    /// <returns>The requested page of goods.</returns>
    ValueTask<AuctionGoodsPage> GetGoodsListAsync(Player player, AuctionGoodsListQuery query);

    /// <summary>
    /// Quotes the price of a quantity of goods, filled cheapest first, without the own listings of the player.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The goods identity and quantity.</param>
    /// <returns>The quotation.</returns>
    ValueTask<AuctionGoodsQuotation> QuoteGoodsAsync(Player player, AuctionGoodsQuoteQuery query);

    /// <summary>
    /// Buys an item listing at the reviewed price.
    /// </summary>
    /// <param name="player">The buying player.</param>
    /// <param name="order">The order.</param>
    /// <returns>The outcome.</returns>
    ValueTask<AuctionBuyListingOutcome> BuyListingAsync(Player player, AuctionBuyListingOrder order);

    /// <summary>
    /// Buys the full quantity of goods at a current total not above the reviewed total.
    /// </summary>
    /// <param name="player">The buying player.</param>
    /// <param name="order">The order.</param>
    /// <returns>The outcome. After <see cref="AuctionHouseResult.QuoteChanged"/> or <see cref="AuctionHouseResult.InsufficientStock"/>, the action sends a fresh quotation.</returns>
    ValueTask<AuctionBuyGoodsOutcome> BuyGoodsAsync(Player player, AuctionBuyGoodsOrder order);

    /// <summary>
    /// Checks whether the item of an inventory slot can be listed.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The inventory slot.</param>
    /// <returns>The outcome.</returns>
    ValueTask<AuctionSellCheckOutcome> CheckSellAsync(Player player, AuctionSellCheckQuery query);

    /// <summary>
    /// Creates a listing of an item or a quantity of goods of the inventory and charges the posting fee.
    /// </summary>
    /// <param name="player">The selling player.</param>
    /// <param name="order">The order, already checked against the current rules.</param>
    /// <returns>The outcome. After <see cref="AuctionHouseResult.RulesChanged"/>, the action sends a fresh broker state.</returns>
    /// <remarks>
    /// The market decides whether the slot holds an item or goods, so it enforces the goods price steps:
    /// a goods unit price which is not on a step of the current <see cref="AuctionHouseRules.GoodsPriceSignificantDigits"/>
    /// (<see cref="AuctionHouseFees.IsOnPriceStep"/>) is answered with <see cref="AuctionHouseResult.PriceNotOnStep"/>, changing nothing.
    /// </remarks>
    ValueTask<AuctionCreateListingOutcome> CreateListingAsync(Player player, AuctionCreateListingOrder order);

    /// <summary>
    /// Cancels an own active listing.
    /// </summary>
    /// <param name="player">The selling player.</param>
    /// <param name="order">The order.</param>
    /// <returns>The outcome.</returns>
    ValueTask<AuctionCancelListingOutcome> CancelListingAsync(Player player, AuctionCancelListingOrder order);

    /// <summary>
    /// Gets the own listings of a player.
    /// </summary>
    /// <param name="player">The selling player.</param>
    /// <returns>The own listings.</returns>
    ValueTask<AuctionOwnListingList> GetOwnListingsAsync(Player player);

    /// <summary>
    /// Gets a page of the collection of a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="query">The requested page.</param>
    /// <returns>The page of the collection.</returns>
    ValueTask<AuctionCollectionPage> GetCollectionAsync(Player player, AuctionCollectionQuery query);

    /// <summary>
    /// Claims one entry or every entry of the collection of a player, as far as inventory space and the inventory Zen limit allow.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="order">The order.</param>
    /// <returns>The outcome.</returns>
    ValueTask<AuctionClaimOutcome> ClaimAsync(Player player, AuctionClaimOrder order);
}
