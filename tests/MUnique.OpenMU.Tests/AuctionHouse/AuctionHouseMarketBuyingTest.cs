// <copyright file="AuctionHouseMarketBuyingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests for buying listings and goods, and quoting goods, of the <see cref="AuctionHouseMarket"/>.
/// </summary>
[TestFixture]
public class AuctionHouseMarketBuyingTest
{
    private const byte Slot = AuctionHouseMarketTestHelper.FirstInventorySlot;

    private AuctionHouseMarketTestHelper _helper = null!;
    private IGameContext _gameContext = null!;
    private AuctionHouseMarket _market = null!;
    private Player _buyer = null!;

    /// <summary>
    /// Creates a market and a buyer with 1,000,000,000 Zen.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._helper = new AuctionHouseMarketTestHelper();
        this._gameContext = this._helper.CreateGameContext();
        this._market = AuctionHouseMarkets.Get(this._gameContext);
        this._buyer = await this._helper.CreatePlayerAsync(this._gameContext, "buyer", 1_000_000_000).ConfigureAwait(false);
    }

    /// <summary>
    /// Buying an item listing charges the buyer once, delivers the item to the inventory, accumulates the seller's
    /// proceeds without touching the seller, records the sale and removes the listing from the index.
    /// </summary>
    [Test]
    public async Task BuyingItemListingSettlesEverythingAsync()
    {
        var (seller, listingId, item) = await this.CreateItemListingAsync(10_000_000).ConfigureAwait(false);
        var sellerMoney = seller.Money;

        var outcome = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Paid, Is.EqualTo(10_000_000));
        Assert.That(outcome.Delivery, Is.EqualTo(AuctionHouseDelivery.Inventory));
        Assert.That(this._buyer.Money, Is.EqualTo(990_000_000));
        Assert.That(this._buyer.Inventory!.Items, Is.EqualTo(new[] { item }));
        Assert.That(seller.Money, Is.EqualTo(sellerMoney));
        Assert.That(this._market.Index.Get(listingId), Is.Null);
        var listing = await this._helper.GetListingAsync(listingId).ConfigureAwait(false);
        Assert.That(listing!.State, Is.EqualTo(AuctionListingState.SoldOut));
        Assert.That(listing.RemainingQuantity, Is.Zero);
        Assert.That(listing.Escrow!.Items, Is.Empty);

        var proceeds = (await this._helper.GetEntriesAsync(seller.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(proceeds.Kind, Is.EqualTo(AuctionHouseCollectionEntryKind.Proceeds));
        Assert.That(proceeds.ListingId, Is.EqualTo(listingId));
        Assert.That(proceeds.Quantity, Is.EqualTo(1));
        Assert.That(proceeds.Gross, Is.EqualTo(10_000_000));
        Assert.That(proceeds.Fee, Is.EqualTo(1_000_000));
        Assert.That(proceeds.UncollectedMoney, Is.EqualTo(9_000_000));
        Assert.That(await this._helper.GetEntriesAsync(this._buyer.SelectedCharacter!).ConfigureAwait(false), Is.Empty);

        var sale = (await this._helper.GetSalesAsync().ConfigureAwait(false)).Single();
        Assert.That(sale.ListingId, Is.EqualTo(listingId));
        Assert.That(sale.Quantity, Is.EqualTo(1));
        Assert.That(sale.UnitPrice, Is.EqualTo(10_000_000));
        Assert.That(sale.Gross, Is.EqualTo(10_000_000));
        Assert.That(sale.Fee, Is.EqualTo(1_000_000));
        Assert.That(sale.SellerCharacterId, Is.EqualTo(seller.SelectedCharacter!.GetId()));
        Assert.That(sale.BuyerCharacterId, Is.EqualTo(this._buyer.SelectedCharacter!.GetId()));
        Assert.That(sale.BuyerName, Is.EqualTo("buyer"));
        Assert.That(sale.SellerName, Is.EqualTo("seller"));
    }

    /// <summary>
    /// A buyer without inventory space gets the item into the collection.
    /// </summary>
    [Test]
    public async Task BuyingWithFullInventoryDeliversToCollectionAsync()
    {
        var (_, listingId, item) = await this.CreateItemListingAsync(10_000).ConfigureAwait(false);
        await this._helper.FillInventoryAsync(this._buyer).ConfigureAwait(false);

        var outcome = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 10_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Delivery, Is.EqualTo(AuctionHouseDelivery.Collection));
        Assert.That(this._buyer.Money, Is.EqualTo(1_000_000_000 - 10_000));
        var entry = (await this._helper.GetEntriesAsync(this._buyer.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(entry.Kind, Is.EqualTo(AuctionHouseCollectionEntryKind.Purchase));
        Assert.That(entry.Quantity, Is.EqualTo(1));
        Assert.That(entry.Escrow!.Items, Is.EqualTo(new[] { item }));
    }

    /// <summary>
    /// The refusals of buying a listing change nothing.
    /// </summary>
    [Test]
    public async Task BuyingListingRefusalsChangeNothingAsync()
    {
        var (seller, listingId, _) = await this.CreateItemListingAsync(10_000_000).ConfigureAwait(false);

        var unknown = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(Guid.NewGuid(), 10_000_000)).ConfigureAwait(false);
        var own = await this._market.BuyListingAsync(seller, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false);
        var changed = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 9_000_000)).ConfigureAwait(false);
        this._buyer.SelectedCharacter!.Inventory!.Money = 9_999_999;
        var poor = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false);

        Assert.That(unknown.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(own.Result, Is.EqualTo(AuctionHouseResult.OwnListing));
        Assert.That(changed.Result, Is.EqualTo(AuctionHouseResult.QuoteChanged));
        Assert.That(poor.Result, Is.EqualTo(AuctionHouseResult.InsufficientZen));
        Assert.That(this._buyer.Money, Is.EqualTo(9_999_999));
        Assert.That(this._market.Index.Get(listingId), Is.Not.Null);
        Assert.That((await this._helper.GetListingAsync(listingId).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Active));
        Assert.That(await this._helper.GetSalesAsync().ConfigureAwait(false), Is.Empty);
    }

    /// <summary>
    /// A sold listing can't be bought again.
    /// </summary>
    [Test]
    public async Task SoldListingIsNotFoundAsync()
    {
        var (_, listingId, _) = await this.CreateItemListingAsync(10_000).ConfigureAwait(false);
        var other = await this._helper.CreatePlayerAsync(this._gameContext, "other", 100_000).ConfigureAwait(false);
        await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 10_000)).ConfigureAwait(false);

        var outcome = await this._market.BuyListingAsync(other, new AuctionBuyListingOrder(listingId, 10_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(other.Money, Is.EqualTo(100_000));
    }

    /// <summary>
    /// The protocol's quote example: 20 Jewels of Life at 2,500,000 and 40 at 2,600,000; 30 cost 76,000,000.
    /// </summary>
    [Test]
    public async Task QuoteFillsAcrossTiersAsync()
    {
        await this.CreateGoodsListingAsync("first", 20, 2_500_000).ConfigureAwait(false);
        await this.CreateGoodsListingAsync("second", 40, 2_600_000).ConfigureAwait(false);

        var quote = await this._market.QuoteGoodsAsync(this._buyer, new AuctionGoodsQuoteQuery(this._helper.JewelOfBlessType, 0, 30)).ConfigureAwait(false);
        var tooMany = await this._market.QuoteGoodsAsync(this._buyer, new AuctionGoodsQuoteQuery(this._helper.JewelOfBlessType, 0, 61)).ConfigureAwait(false);

        Assert.That(quote.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(quote.FillableQuantity, Is.EqualTo(30));
        Assert.That(quote.Total, Is.EqualTo(76_000_000));
        Assert.That(quote.Tiers, Is.EqualTo(new[] { new AuctionPriceTier(2_500_000, 20), new AuctionPriceTier(2_600_000, 40) }));
        Assert.That(tooMany.Result, Is.EqualTo(AuctionHouseResult.InsufficientStock));
        Assert.That(tooMany.FillableQuantity, Is.EqualTo(60));
    }

    /// <summary>
    /// A goods purchase takes the cheapest units first, across listings, with one sale record and one proceeds entry
    /// per listing, and leaves the rest of a partly sold listing active.
    /// </summary>
    [Test]
    public async Task BuyingGoodsFillsCheapestFirstAcrossListingsAsync()
    {
        var (expensiveSeller, expensive) = await this.CreateGoodsListingAsync("dear", 40, 2_600_000).ConfigureAwait(false);
        var (cheapSeller, cheap) = await this.CreateGoodsListingAsync("cheap", 20, 2_500_000).ConfigureAwait(false);

        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 30, 76_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Quantity, Is.EqualTo(30));
        Assert.That(outcome.Paid, Is.EqualTo(76_000_000));
        Assert.That(outcome.Delivered, Is.EqualTo(30));
        Assert.That(outcome.Kept, Is.Zero);
        Assert.That(this._buyer.Money, Is.EqualTo(1_000_000_000 - 76_000_000));
        Assert.That(this._buyer.Inventory!.Items.Count(item => item.Definition == this._helper.JewelOfBless), Is.EqualTo(30));
        Assert.That(this._market.Index.Get(cheap), Is.Null);
        Assert.That(this._market.Index.Get(expensive)?.RemainingQuantity, Is.EqualTo(30));
        Assert.That((await this._helper.GetListingAsync(expensive).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Active));

        var sales = await this._helper.GetSalesAsync().ConfigureAwait(false);
        Assert.That(sales.Select(sale => (sale.ListingId, sale.Quantity)), Is.EquivalentTo(new[] { (cheap, 20), (expensive, 10) }));
        var cheapProceeds = (await this._helper.GetEntriesAsync(cheapSeller.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(cheapProceeds.Gross, Is.EqualTo(50_000_000));
        Assert.That(cheapProceeds.UncollectedMoney, Is.EqualTo(45_000_000));
        var expensiveProceeds = (await this._helper.GetEntriesAsync(expensiveSeller.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(expensiveProceeds.Quantity, Is.EqualTo(10));
        Assert.That(expensiveProceeds.UncollectedMoney, Is.EqualTo(23_400_000));
    }

    /// <summary>
    /// At equal prices, only the older listing is reduced (the spec's "equal prices" scenario).
    /// </summary>
    [Test]
    public async Task BuyingGoodsAtEqualPriceTakesOlderListingFirstAsync()
    {
        var time = new ManualTimeProvider();
        var market = new AuctionHouseMarket(this._helper.ContextProvider, this._helper.Configuration, new Microsoft.Extensions.Logging.Abstractions.NullLogger<AuctionHouseMarket>(), time);
        var (_, older) = await this.CreateGoodsListingAsync("older", 10, 2_000_000, market).ConfigureAwait(false);
        time.Advance(TimeSpan.FromMinutes(1));
        var (_, newer) = await this.CreateGoodsListingAsync("newer", 10, 2_000_000, market).ConfigureAwait(false);

        var outcome = await market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 4, 8_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(market.Index.Get(older)?.RemainingQuantity, Is.EqualTo(6));
        Assert.That(market.Index.Get(newer)?.RemainingQuantity, Is.EqualTo(10));
    }

    /// <summary>
    /// The buyer's own listings are never part of a goods purchase.
    /// </summary>
    [Test]
    public async Task BuyingGoodsSkipsOwnListingsAsync()
    {
        await this._helper.AddItemsAsync(this._buyer, this._helper.JewelOfBless, 5).ConfigureAwait(false);
        var own = await this._market.CreateListingAsync(this._buyer, new AuctionCreateListingOrder(Slot, 5, 1_000_000, 48, 0)).ConfigureAwait(false);
        var (_, other) = await this.CreateGoodsListingAsync("other", 5, 2_000_000).ConfigureAwait(false);

        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 5, 10_000_000)).ConfigureAwait(false);
        var tooMany = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 1, 10_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Paid, Is.EqualTo(10_000_000));
        Assert.That(this._market.Index.Get(own.ListingId)?.RemainingQuantity, Is.EqualTo(5));
        Assert.That(this._market.Index.Get(other), Is.Null);
        Assert.That(tooMany.Result, Is.EqualTo(AuctionHouseResult.InsufficientStock));
    }

    /// <summary>
    /// A purchase above the reviewed total is refused; a lower current total is charged.
    /// </summary>
    [Test]
    public async Task BuyingGoodsComparesWithReviewedTotalAsync()
    {
        await this.CreateGoodsListingAsync("seller", 10, 2_000_000).ConfigureAwait(false);

        var higher = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 5, 9_999_999)).ConfigureAwait(false);
        var lower = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 5, 12_000_000)).ConfigureAwait(false);

        Assert.That(higher.Result, Is.EqualTo(AuctionHouseResult.QuoteChanged));
        Assert.That(lower.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(lower.Paid, Is.EqualTo(10_000_000));
        Assert.That(this._buyer.Money, Is.EqualTo(990_000_000));
    }

    /// <summary>
    /// A buyer without enough Zen is refused.
    /// </summary>
    [Test]
    public async Task BuyingGoodsWithoutEnoughZenIsRefusedAsync()
    {
        await this.CreateGoodsListingAsync("seller", 10, 2_000_000).ConfigureAwait(false);
        this._buyer.SelectedCharacter!.Inventory!.Money = 3_999_999;

        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 2, 4_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.InsufficientZen));
        Assert.That(this._buyer.Money, Is.EqualTo(3_999_999));
        Assert.That(this._buyer.Inventory!.Items, Is.Empty);
    }

    /// <summary>
    /// When the index promised units which the locked rows don't hold anymore, the purchase is refused and the index
    /// is corrected.
    /// </summary>
    [Test]
    public async Task StaleIndexIsRefusedAndCorrectedAsync()
    {
        var (_, listingId) = await this.CreateGoodsListingAsync("seller", 10, 2_000_000).ConfigureAwait(false);
        var listing = await this._helper.GetListingAsync(listingId).ConfigureAwait(false);
        listing!.State = AuctionListingState.Cancelled;

        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 5, 10_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.InsufficientStock));
        Assert.That(this._market.Index.Get(listingId), Is.Null);
        Assert.That(this._buyer.Money, Is.EqualTo(1_000_000_000));
    }

    /// <summary>
    /// The spec's default fees: a seller lists 10 Jewels of Bless at 3,000,000 Zen each, all are sold; the seller
    /// paid 1,500,000 Zen posting fee and receives 27,000,000 Zen proceeds.
    /// </summary>
    [Test]
    public async Task DefaultFeesAsync()
    {
        var (seller, listingId) = await this.CreateGoodsListingAsync("seller", 10, 3_000_000).ConfigureAwait(false);
        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 4, 12_000_000)).ConfigureAwait(false);
        var secondOutcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 6, 18_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(secondOutcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(seller.Money, Is.EqualTo(100_000_000 - 1_500_000));
        var proceeds = (await this._helper.GetEntriesAsync(seller.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(proceeds.Quantity, Is.EqualTo(10));
        Assert.That(proceeds.Gross, Is.EqualTo(30_000_000));
        Assert.That(proceeds.Fee, Is.EqualTo(3_000_000));
        Assert.That(proceeds.UncollectedMoney, Is.EqualTo(27_000_000));
        Assert.That((await this._helper.GetListingAsync(listingId).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.SoldOut));
        Assert.That(await this._helper.GetSalesAsync().ConfigureAwait(false), Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Goods which don't fit into the inventory are kept in the purchase entry.
    /// </summary>
    [Test]
    public async Task BuyingGoodsWithLittleSpaceKeepsTheRestAsync()
    {
        await this.CreateGoodsListingAsync("seller", 10, 2_000_000).ConfigureAwait(false);
        await this._helper.FillInventoryAsync(this._buyer).ConfigureAwait(false);
        var freed = this._buyer.Inventory!.Items.Take(3).ToList();
        foreach (var item in freed)
        {
            await this._buyer.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 10, 20_000_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Delivered, Is.EqualTo(3));
        Assert.That(outcome.Kept, Is.EqualTo(7));
        var entry = (await this._helper.GetEntriesAsync(this._buyer.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(entry.Quantity, Is.EqualTo(7));
        Assert.That(entry.Escrow!.Items, Has.Count.EqualTo(7));
    }

    private async ValueTask<(Player Seller, Guid ListingId, Item Item)> CreateItemListingAsync(uint price)
    {
        var seller = await this._helper.CreatePlayerAsync(this._gameContext, "seller", 100_000_000).ConfigureAwait(false);
        var item = await this._helper.AddItemAsync(seller, this._helper.Kris, Slot).ConfigureAwait(false);
        var outcome = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, price, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        return (seller, outcome.ListingId, item);
    }

    private async ValueTask<(Player Seller, Guid ListingId)> CreateGoodsListingAsync(string sellerName, int quantity, uint unitPrice, AuctionHouseMarket? market = null)
    {
        var seller = await this._helper.CreatePlayerAsync(this._gameContext, sellerName, 100_000_000).ConfigureAwait(false);
        await this._helper.AddItemsAsync(seller, this._helper.JewelOfBless, quantity).ConfigureAwait(false);
        var outcome = await (market ?? this._market).CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, (uint)quantity, unitPrice, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        return (seller, outcome.ListingId);
    }

    /// <summary>
    /// A time provider whose time is advanced by the test.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this._now;

        public void Advance(TimeSpan duration) => this._now += duration;
    }
}
