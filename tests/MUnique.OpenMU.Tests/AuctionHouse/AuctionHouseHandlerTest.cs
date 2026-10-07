// <copyright file="AuctionHouseHandlerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Moq;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

/// <summary>
/// Tests that the auction house packet handlers parse the request fixtures and pass the intended arguments
/// through the action to the market, and that malformed packets are ignored.
/// </summary>
[TestFixture]
public class AuctionHouseHandlerTest
{
    private const ushort JewelOfBless = (14 * 512) + 13;
    private const ushort JewelOfSoul = (14 * 512) + 14;
    private const ushort Armor = (8 * 512) + 9;

    private FakeAuctionHouseMarket _market = null!;
    private Player _player = null!;

    /// <summary>
    /// Creates a player with an open broker and a fake market.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._market = new FakeAuctionHouseMarket();
        this._player = await AuctionHouseTestHelper.CreatePlayerAsync(this._market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(this._player).ConfigureAwait(false);
        this._market.Calls.Clear();
    }

    /// <summary>
    /// The handlers belong to the custom packet group and have the sub codes of the registry.
    /// </summary>
    [Test]
    public void HandlersHaveRegistrySubCodes()
    {
        var keys = AllHandlers().Select(handler => handler.Key).ToArray();

        Assert.That(keys, Is.EqualTo(new byte[] { 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5C }));
        foreach (var handler in AllHandlers())
        {
            var group = handler.GetType().GetCustomAttributes(typeof(BelongsToGroupAttribute), false).Cast<BelongsToGroupAttribute>().Single();
            Assert.That(group.GroupKey, Is.EqualTo(CustomGroupHandlerPlugIn.GroupKey));
        }
    }

    /// <summary>
    /// The search request with every filter, name and ancient entries reaches the market as query.
    /// </summary>
    [Test]
    public async Task SearchRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionSearchRequestHandlerPlugIn(), "auction-search-request.bin").ConfigureAwait(false);

        var query = this._market.SingleArgument<AuctionSearchQuery>(nameof(IAuctionHouseMarket.SearchAsync));
        Assert.That(query.Page, Is.EqualTo(2));
        Assert.That(query.Sort, Is.EqualTo(AuctionHouseSortOrder.PriceDescending));
        Assert.That(query.Slots, Is.EqualTo(AuctionHouseEquipmentSlots.Weapon));
        Assert.That(query.GroupMask, Is.EqualTo(0x0001));
        Assert.That(query.ClassLine, Is.EqualTo(AuctionHouseClassLine.DarkKnight));
        Assert.That(query.Options, Is.EqualTo(AuctionHouseSearchOptions.CanEquipNow | AuctionHouseSearchOptions.Luck | AuctionHouseSearchOptions.Skill));
        Assert.That(query.Family, Is.EqualTo(AuctionHouseItemFamily.Excellent));
        Assert.That(query.UpgradeMin, Is.EqualTo(7));
        Assert.That(query.UpgradeMax, Is.EqualTo(15));
        Assert.That(query.ExcellentMask, Is.EqualTo(0x09));
        Assert.That(query.MinExcellentCount, Is.EqualTo(2));
        Assert.That(query.MinSockets, Is.EqualTo(0));
        Assert.That(query.MaxPrice, Is.EqualTo(2_147_483_647));
        Assert.That(query.NameMatchTypes, Is.EqualTo(new ushort[] { 5, 26 }));
        Assert.That(query.AncientSetMembers, Is.EqualTo(new[] { new AuctionAncientSetMember(Armor, 1), new AuctionAncientSetMember(Armor, 2) }));
        this._player.View<IShowAuctionSearchResultPlugIn>().Verify(v => v.ShowAuctionSearchResultAsync(0x1234, It.IsAny<AuctionSearchPage>()), Times.Once);
    }

    /// <summary>
    /// The search request without filters and entries reaches the market as query.
    /// </summary>
    [Test]
    public async Task EmptySearchRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionSearchRequestHandlerPlugIn(), "auction-search-request-empty.bin").ConfigureAwait(false);

        var query = this._market.SingleArgument<AuctionSearchQuery>(nameof(IAuctionHouseMarket.SearchAsync));
        Assert.That(query.ClassLine, Is.EqualTo(AuctionHouseClassLine.Any));
        Assert.That(query.Family, Is.EqualTo(AuctionHouseItemFamily.Any));
        Assert.That(query.UpgradeMax, Is.EqualTo(15));
        Assert.That(query.NameMatchTypes, Is.Empty);
        Assert.That(query.AncientSetMembers, Is.Empty);
        this._player.View<IShowAuctionSearchResultPlugIn>().Verify(v => v.ShowAuctionSearchResultAsync(1, It.IsAny<AuctionSearchPage>()), Times.Once);
    }

    /// <summary>
    /// The goods list requests reach the market as query.
    /// </summary>
    [Test]
    public async Task GoodsListRequestsReachMarketAsync()
    {
        await HandleFixtureAsync(new AuctionGoodsListRequestHandlerPlugIn(), "auction-goods-list-request-all.bin").ConfigureAwait(false);
        await HandleFixtureAsync(new AuctionGoodsListRequestHandlerPlugIn(), "auction-goods-list-request.bin").ConfigureAwait(false);

        var queries = this._market.Calls.Select(call => (AuctionGoodsListQuery)call.Argument!).ToList();
        Assert.That(queries[0].Page, Is.EqualTo(0));
        Assert.That(queries[0].ItemTypes, Is.Empty);
        Assert.That(queries[1].Page, Is.EqualTo(1));
        Assert.That(queries[1].ItemTypes, Is.EqualTo(new[] { JewelOfBless, JewelOfSoul }));
        var view = this._player.View<IShowAuctionGoodsListPlugIn>();
        view.Verify(v => v.ShowAuctionGoodsListAsync(2, It.IsAny<AuctionGoodsPage>()), Times.Once);
        view.Verify(v => v.ShowAuctionGoodsListAsync(3, It.IsAny<AuctionGoodsPage>()), Times.Once);
    }

    /// <summary>
    /// The goods quote request reaches the market as query.
    /// </summary>
    [Test]
    public async Task GoodsQuoteRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionGoodsQuoteRequestHandlerPlugIn(), "auction-goods-quote-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionGoodsQuoteQuery>(nameof(IAuctionHouseMarket.QuoteGoodsAsync)), Is.EqualTo(new AuctionGoodsQuoteQuery(JewelOfBless, 0, 30)));
        this._player.View<IShowAuctionGoodsQuotePlugIn>().Verify(v => v.ShowAuctionGoodsQuoteAsync(4, It.IsAny<AuctionGoodsQuotation>()), Times.Once);
    }

    /// <summary>
    /// The buy listing request reaches the market as order.
    /// </summary>
    [Test]
    public async Task BuyListingRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionBuyListingRequestHandlerPlugIn(), "auction-buy-listing-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionBuyListingOrder>(nameof(IAuctionHouseMarket.BuyListingAsync)), Is.EqualTo(new AuctionBuyListingOrder(Id(0x00), 1_000_000_000)));
        this._player.View<IShowAuctionBuyListingResultPlugIn>().Verify(v => v.ShowAuctionBuyListingResultAsync(6, It.IsAny<AuctionBuyListingOutcome>()), Times.Once);
    }

    /// <summary>
    /// The buy goods request reaches the market as order.
    /// </summary>
    [Test]
    public async Task BuyGoodsRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionBuyGoodsRequestHandlerPlugIn(), "auction-buy-goods-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionBuyGoodsOrder>(nameof(IAuctionHouseMarket.BuyGoodsAsync)), Is.EqualTo(new AuctionBuyGoodsOrder(JewelOfBless, 0, 18, 18_000_000)));
        this._player.View<IShowAuctionBuyGoodsResultPlugIn>().Verify(v => v.ShowAuctionBuyGoodsResultAsync(7, It.IsAny<AuctionBuyGoodsOutcome>()), Times.Once);
    }

    /// <summary>
    /// The create listing request with the posting fee of the current rules reaches the market as order.
    /// </summary>
    [Test]
    public async Task CreateListingRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionCreateListingRequestHandlerPlugIn(), "auction-create-listing-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionCreateListingOrder>(nameof(IAuctionHouseMarket.CreateListingAsync)), Is.EqualTo(new AuctionCreateListingOrder(12, 18, 1_000_000, 24, 181_000)));
        this._player.View<IShowAuctionCreateListingResultPlugIn>().Verify(v => v.ShowAuctionCreateListingResultAsync(8, It.IsAny<AuctionCreateListingOutcome>()), Times.Once);
    }

    /// <summary>
    /// The cancel listing request reaches the market as order.
    /// </summary>
    [Test]
    public async Task CancelListingRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionCancelListingRequestHandlerPlugIn(), "auction-cancel-listing-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionCancelListingOrder>(nameof(IAuctionHouseMarket.CancelListingAsync)), Is.EqualTo(new AuctionCancelListingOrder(Id(0x20))));
        this._player.View<IShowAuctionCancelListingResultPlugIn>().Verify(v => v.ShowAuctionCancelListingResultAsync(9, It.IsAny<AuctionCancelListingOutcome>()), Times.Once);
    }

    /// <summary>
    /// The own listings request reaches the market.
    /// </summary>
    [Test]
    public async Task OwnListingsRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionOwnListingsRequestHandlerPlugIn(), "auction-own-listings-request.bin").ConfigureAwait(false);

        Assert.That(this._market.Calls.Single().Method, Is.EqualTo(nameof(IAuctionHouseMarket.GetOwnListingsAsync)));
        this._player.View<IShowAuctionOwnListingsPlugIn>().Verify(v => v.ShowAuctionOwnListingsAsync(10, It.IsAny<AuctionOwnListingList>()), Times.Once);
    }

    /// <summary>
    /// The collection request reaches the market as query.
    /// </summary>
    [Test]
    public async Task CollectionRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionCollectionRequestHandlerPlugIn(), "auction-collection-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionCollectionQuery>(nameof(IAuctionHouseMarket.GetCollectionAsync)), Is.EqualTo(new AuctionCollectionQuery(1)));
        this._player.View<IShowAuctionCollectionPlugIn>().Verify(v => v.ShowAuctionCollectionAsync(12, It.IsAny<AuctionCollectionPage>()), Times.Once);
    }

    /// <summary>
    /// The claim requests reach the market as order; an all-zero entry id means every entry.
    /// </summary>
    [Test]
    public async Task ClaimRequestsReachMarketAsync()
    {
        await HandleFixtureAsync(new AuctionClaimRequestHandlerPlugIn(), "auction-claim-request-all.bin").ConfigureAwait(false);
        await HandleFixtureAsync(new AuctionClaimRequestHandlerPlugIn(), "auction-claim-request.bin").ConfigureAwait(false);

        var orders = this._market.Calls.Select(call => (AuctionClaimOrder)call.Argument!).ToList();
        Assert.That(orders, Is.EqualTo(new[] { new AuctionClaimOrder(null), new AuctionClaimOrder(Id(0x50)) }));
        var view = this._player.View<IShowAuctionClaimResultPlugIn>();
        view.Verify(v => v.ShowAuctionClaimResultAsync(14, It.IsAny<AuctionClaimOutcome>()), Times.Once);
        view.Verify(v => v.ShowAuctionClaimResultAsync(15, It.IsAny<AuctionClaimOutcome>()), Times.Once);
    }

    /// <summary>
    /// The sell check request reaches the market as query.
    /// </summary>
    [Test]
    public async Task SellCheckRequestReachesMarketAsync()
    {
        await HandleFixtureAsync(new AuctionSellCheckRequestHandlerPlugIn(), "auction-sell-check-request.bin").ConfigureAwait(false);

        Assert.That(this._market.SingleArgument<AuctionSellCheckQuery>(nameof(IAuctionHouseMarket.CheckSellAsync)), Is.EqualTo(new AuctionSellCheckQuery(12)));
        this._player.View<IShowAuctionSellCheckResultPlugIn>().Verify(v => v.ShowAuctionSellCheckResultAsync(16, It.IsAny<AuctionSellCheckOutcome>()), Times.Once);
    }

    /// <summary>
    /// A request packet which is one byte shorter or longer than its layout is ignored: no answer and no market call.
    /// </summary>
    /// <param name="fixtureName">The name of the request fixture.</param>
    /// <param name="lengthChange">The change of the packet length.</param>
    [TestCase("auction-search-request.bin", -1)]
    [TestCase("auction-search-request.bin", -3)]
    [TestCase("auction-search-request.bin", 3)]
    [TestCase("auction-search-request-empty.bin", -1)]
    [TestCase("auction-goods-list-request.bin", -2)]
    [TestCase("auction-goods-list-request-all.bin", -1)]
    [TestCase("auction-goods-quote-request.bin", -1)]
    [TestCase("auction-buy-listing-request.bin", -1)]
    [TestCase("auction-buy-goods-request.bin", -1)]
    [TestCase("auction-create-listing-request.bin", -1)]
    [TestCase("auction-create-listing-request.bin", 1)]
    [TestCase("auction-cancel-listing-request.bin", -1)]
    [TestCase("auction-own-listings-request.bin", -1)]
    [TestCase("auction-collection-request.bin", -1)]
    [TestCase("auction-claim-request.bin", -1)]
    [TestCase("auction-sell-check-request.bin", -1)]
    public async Task MalformedPacketIsIgnoredAsync(string fixtureName, int lengthChange)
    {
        var fixture = AuctionHousePacketFixtureTest.ReadFixture(fixtureName);
        var packet = new byte[fixture.Length + lengthChange];
        fixture.AsSpan(0, Math.Min(fixture.Length, packet.Length)).CopyTo(packet);
        packet[1] = (byte)packet.Length;
        var handler = AllHandlers().Single(h => h.Key == fixture[3]);

        await handler.HandlePacketAsync(this._player, packet).ConfigureAwait(false);

        Assert.That(this._market.Calls, Is.Empty);
        AssertNoAnswer(this._player);
    }

    private static IEnumerable<ISubPacketHandlerPlugIn> AllHandlers()
    {
        yield return new AuctionSearchRequestHandlerPlugIn();
        yield return new AuctionGoodsListRequestHandlerPlugIn();
        yield return new AuctionGoodsQuoteRequestHandlerPlugIn();
        yield return new AuctionBuyListingRequestHandlerPlugIn();
        yield return new AuctionBuyGoodsRequestHandlerPlugIn();
        yield return new AuctionCreateListingRequestHandlerPlugIn();
        yield return new AuctionCancelListingRequestHandlerPlugIn();
        yield return new AuctionOwnListingsRequestHandlerPlugIn();
        yield return new AuctionCollectionRequestHandlerPlugIn();
        yield return new AuctionClaimRequestHandlerPlugIn();
        yield return new AuctionSellCheckRequestHandlerPlugIn();
    }

    private static void AssertNoAnswer(Player player)
    {
        player.View<IShowAuctionSearchResultPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionGoodsListPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionGoodsQuotePlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionBuyListingResultPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionBuyGoodsResultPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionCreateListingResultPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionCancelListingResultPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionOwnListingsPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionCollectionPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionClaimResultPlugIn>().VerifyNoOtherCalls();
        player.View<IShowAuctionSellCheckResultPlugIn>().VerifyNoOtherCalls();
    }

    private static Guid Id(byte start) => new(Enumerable.Range(start, 16).Select(value => (byte)value).ToArray());

    private async Task HandleFixtureAsync(ISubPacketHandlerPlugIn handler, string fixtureName)
    {
        var fixture = AuctionHousePacketFixtureTest.ReadFixture(fixtureName);
        await handler.HandlePacketAsync(this._player, fixture).ConfigureAwait(false);
    }
}
