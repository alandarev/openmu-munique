// <copyright file="FakeAuctionHouseMarket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A market for tests which records the received requests and returns configurable results.
/// </summary>
internal sealed class FakeAuctionHouseMarket : IAuctionHouseMarket
{
    /// <summary>
    /// Gets the received calls, as method name and argument, in their order.
    /// </summary>
    public List<(string Method, object? Argument)> Calls { get; } = new();

    /// <summary>
    /// Gets or sets a task which every call awaits before it returns; used to hold a request in the market.
    /// </summary>
    public Task Gate { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Gets or sets the seller summary.
    /// </summary>
    public AuctionSellerSummary SellerSummary { get; set; } = new(3, 2);

    /// <summary>
    /// Gets or sets the search page.
    /// </summary>
    public AuctionSearchPage SearchPage { get; set; } = new(AuctionHouseResult.Success, 0, 0, []);

    /// <summary>
    /// Gets or sets the goods page.
    /// </summary>
    public AuctionGoodsPage GoodsPage { get; set; } = new(AuctionHouseResult.Success, 0, 0, []);

    /// <summary>
    /// Gets or sets the goods quotation.
    /// </summary>
    public AuctionGoodsQuotation Quotation { get; set; } = new(AuctionHouseResult.Success, 0, 0, 0, 0, 0, []);

    /// <summary>
    /// Gets or sets the outcome of buying a listing.
    /// </summary>
    public AuctionBuyListingOutcome BuyListingOutcome { get; set; } = new(AuctionHouseResult.Success, Guid.Empty, 0, AuctionHouseDelivery.Inventory);

    /// <summary>
    /// Gets or sets the outcome of buying goods.
    /// </summary>
    public AuctionBuyGoodsOutcome BuyGoodsOutcome { get; set; } = new(AuctionHouseResult.Success, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Gets or sets the outcome of a sell check.
    /// </summary>
    public AuctionSellCheckOutcome SellCheckOutcome { get; set; } = new(AuctionHouseResult.Success, 0, AuctionHouseSellCheckReason.Eligible, AuctionHouseListingKind.Item, 0, 0, 0, 0, 0);

    /// <summary>
    /// Gets or sets the outcome of creating a listing.
    /// </summary>
    public AuctionCreateListingOutcome CreateListingOutcome { get; set; } = new(AuctionHouseResult.Success, Guid.Empty, 0, 0);

    /// <summary>
    /// Gets or sets the outcome of cancelling a listing.
    /// </summary>
    public AuctionCancelListingOutcome CancelListingOutcome { get; set; } = new(AuctionHouseResult.Success, Guid.Empty, 0, 0, 0);

    /// <summary>
    /// Gets or sets the own listings.
    /// </summary>
    public AuctionOwnListingList OwnListings { get; set; } = new(AuctionHouseResult.Success, []);

    /// <summary>
    /// Gets or sets the collection page.
    /// </summary>
    public AuctionCollectionPage CollectionPage { get; set; } = new(AuctionHouseResult.Success, 0, 0, []);

    /// <summary>
    /// Gets or sets the outcome of a claim.
    /// </summary>
    public AuctionClaimOutcome ClaimOutcome { get; set; } = new(AuctionHouseResult.Success, 0, 0, 0, 0, AuctionHouseKeptBack.None);

    /// <summary>
    /// Gets the argument of the single received call of a method.
    /// </summary>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <param name="method">The method name.</param>
    /// <returns>The argument.</returns>
    public T SingleArgument<T>(string method) => (T)this.Calls.Single(call => call.Method == method).Argument!;

    /// <inheritdoc />
    public ValueTask<AuctionSellerSummary> GetSellerSummaryAsync(Player player) => this.ReturnAsync(nameof(this.GetSellerSummaryAsync), null, this.SellerSummary);

    /// <inheritdoc />
    public ValueTask<AuctionSearchPage> SearchAsync(Player player, AuctionSearchQuery query) => this.ReturnAsync(nameof(this.SearchAsync), query, this.SearchPage);

    /// <inheritdoc />
    public ValueTask<AuctionGoodsPage> GetGoodsListAsync(Player player, AuctionGoodsListQuery query) => this.ReturnAsync(nameof(this.GetGoodsListAsync), query, this.GoodsPage);

    /// <inheritdoc />
    public ValueTask<AuctionGoodsQuotation> QuoteGoodsAsync(Player player, AuctionGoodsQuoteQuery query) => this.ReturnAsync(nameof(this.QuoteGoodsAsync), query, this.Quotation);

    /// <inheritdoc />
    public ValueTask<AuctionBuyListingOutcome> BuyListingAsync(Player player, AuctionBuyListingOrder order) => this.ReturnAsync(nameof(this.BuyListingAsync), order, this.BuyListingOutcome);

    /// <inheritdoc />
    public ValueTask<AuctionBuyGoodsOutcome> BuyGoodsAsync(Player player, AuctionBuyGoodsOrder order) => this.ReturnAsync(nameof(this.BuyGoodsAsync), order, this.BuyGoodsOutcome);

    /// <inheritdoc />
    public ValueTask<AuctionSellCheckOutcome> CheckSellAsync(Player player, AuctionSellCheckQuery query) => this.ReturnAsync(nameof(this.CheckSellAsync), query, this.SellCheckOutcome);

    /// <inheritdoc />
    public ValueTask<AuctionCreateListingOutcome> CreateListingAsync(Player player, AuctionCreateListingOrder order) => this.ReturnAsync(nameof(this.CreateListingAsync), order, this.CreateListingOutcome);

    /// <inheritdoc />
    public ValueTask<AuctionCancelListingOutcome> CancelListingAsync(Player player, AuctionCancelListingOrder order) => this.ReturnAsync(nameof(this.CancelListingAsync), order, this.CancelListingOutcome);

    /// <inheritdoc />
    public ValueTask<AuctionOwnListingList> GetOwnListingsAsync(Player player) => this.ReturnAsync(nameof(this.GetOwnListingsAsync), null, this.OwnListings);

    /// <inheritdoc />
    public ValueTask<AuctionCollectionPage> GetCollectionAsync(Player player, AuctionCollectionQuery query) => this.ReturnAsync(nameof(this.GetCollectionAsync), query, this.CollectionPage);

    /// <inheritdoc />
    public ValueTask<AuctionClaimOutcome> ClaimAsync(Player player, AuctionClaimOrder order) => this.ReturnAsync(nameof(this.ClaimAsync), order, this.ClaimOutcome);

    private async ValueTask<T> ReturnAsync<T>(string method, object? argument, T result)
    {
        lock (this.Calls)
        {
            this.Calls.Add((method, argument));
        }

        await this.Gate.ConfigureAwait(false);
        return result;
    }
}
