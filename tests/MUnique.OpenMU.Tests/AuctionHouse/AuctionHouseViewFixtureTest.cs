// <copyright file="AuctionHouseViewFixtureTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.GameServer.RemoteView.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests that the auction house views write exactly the golden fixtures for equivalent results.
/// </summary>
[TestFixture]
public class AuctionHouseViewFixtureTest
{
    private const ushort JewelOfBless = (14 * 512) + 13;
    private const ushort JewelOfSoul = (14 * 512) + 14;
    private const ushort Blade = 5;
    private const ushort Armor = (8 * 512) + 9;

    private static readonly byte[] BladeItemData = [0x00, 0x05, 0x09, 0x2A, 0x0F, 0x03, 0x09];
    private static readonly byte[] AncientArmorItemData = [0x80, 0x09, 0x07, 0x2D, 0x13, 0x02, 0x51];
    private static readonly byte[] SocketSwordItemData = [0x00, 0x1A, 0x0B, 0x40, 0x85, 0x01, 0x03, 0x01, 0xFE, 0xFE];
    private static readonly byte[] ExcellentDragonArmorItemData = [0x80, 0x01, 0x09, 0x61, 0x08, 0x3F];

    private readonly Item _blade = new Persistence.BasicModel.Item { Id = Guid.NewGuid() };
    private readonly Item _ancientArmor = new Persistence.BasicModel.Item { Id = Guid.NewGuid() };
    private readonly Item _socketSword = new Persistence.BasicModel.Item { Id = Guid.NewGuid() };
    private readonly Item _excellentDragonArmor = new Persistence.BasicModel.Item { Id = Guid.NewGuid() };
    private ItemSerializerStub _itemSerializer = null!;

    /// <summary>
    /// Sets up the item serializer, which returns the fixture item data for the test items.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._itemSerializer = new ItemSerializerStub(new Dictionary<Item, byte[]>
        {
            { this._blade, BladeItemData },
            { this._ancientArmor, AncientArmorItemData },
            { this._socketSword, SocketSwordItemData },
            { this._excellentDragonArmor, ExcellentDragonArmorItemData },
        });
    }

    /// <summary>
    /// The open broker state is written as the fixture.
    /// </summary>
    [Test]
    public void StateOpenMatchesFixture()
    {
        var rules = new AuctionHouseRules
        {
            PageSize = 20,
            MaximumActiveListings = 30,
            MaximumPrice = 2_000_000_000,
            PostingFeeFlat = 1_000,
            PostingFeeBasisPoints = 100,
            SaleFeeBasisPoints = 500,
            RefundPostingFeeOnCancel = true,
            DurationHours = [12, 24, 48, 72],
            PriceStepPrecision = 3,
        };
        var state = AuctionBrokerState.Create(rules, int.MaxValue, new AuctionSellerSummary(3, 2));

        AssertWritten("auction-house-state-open.bin", target => ShowAuctionHouseStatePlugIn.Write(target, state));
    }

    /// <summary>
    /// The closed broker state is written as the fixture.
    /// </summary>
    [Test]
    public void StateClosedMatchesFixture()
    {
        AssertWritten("auction-house-state-closed.bin", target => ShowAuctionHouseStatePlugIn.Write(target, null));
    }

    /// <summary>
    /// The empty search result is written as the fixture.
    /// </summary>
    [Test]
    public void SearchResultEmptyMatchesFixture()
    {
        var page = new AuctionSearchPage(AuctionHouseResult.Success, 0, 0, []);
        AssertWritten("auction-search-result-empty.bin", target => ShowAuctionSearchResultPlugIn.Write(target, 1, page, this._itemSerializer));
    }

    /// <summary>
    /// The search result with two listings of different item data lengths is written as the fixture.
    /// </summary>
    [Test]
    public void SearchResultMatchesFixture()
    {
        var page = new AuctionSearchPage(AuctionHouseResult.Success, 1, 45, [
            new AuctionListingRow(Id(0x00), 1_000_000_000, 86_400, "Seller", true, false, this._blade),
            new AuctionListingRow(Id(0x10), 2_147_483_647, 3_600, "Merchant10", true, true, this._socketSword),
        ]);
        AssertWritten("auction-search-result.bin", target => ShowAuctionSearchResultPlugIn.Write(target, 0x1234, page, this._itemSerializer));
    }

    /// <summary>
    /// The empty goods list is written as the fixture.
    /// </summary>
    [Test]
    public void GoodsListEmptyMatchesFixture()
    {
        var page = new AuctionGoodsPage(AuctionHouseResult.Success, 0, 0, []);
        AssertWritten("auction-goods-list-empty.bin", target => ShowAuctionGoodsListPlugIn.Write(target, 2, page));
    }

    /// <summary>
    /// The goods list with two goods is written as the fixture.
    /// </summary>
    [Test]
    public void GoodsListMatchesFixture()
    {
        var page = new AuctionGoodsPage(AuctionHouseResult.Success, 1, 27, [
            new AuctionGoodsRow(JewelOfBless, 0, 1_000_000, 18, 3),
            new AuctionGoodsRow(JewelOfSoul, 0, 2_147_483_647, 5, 1),
        ]);
        AssertWritten("auction-goods-list.bin", target => ShowAuctionGoodsListPlugIn.Write(target, 3, page));
    }

    /// <summary>
    /// The quotation without stock is written as the fixture.
    /// </summary>
    [Test]
    public void GoodsQuoteEmptyMatchesFixture()
    {
        var quotation = new AuctionGoodsQuotation(AuctionHouseResult.InsufficientStock, JewelOfSoul, 0, 5, 0, 0, []);
        AssertWritten("auction-goods-quote-empty.bin", target => ShowAuctionGoodsQuotePlugIn.Write(target, 5, quotation));
    }

    /// <summary>
    /// The quotation with eight price tiers is written as the fixture.
    /// </summary>
    [Test]
    public void GoodsQuoteMatchesFixture()
    {
        var quantities = new uint[] { 5, 4, 4, 4, 4, 3, 3, 3 };
        var tiers = quantities.Select((quantity, i) => new AuctionPriceTier((uint)(1_000_000 + (50_000 * i)), quantity)).ToList();
        var quotation = new AuctionGoodsQuotation(AuctionHouseResult.Success, JewelOfBless, 0, 30, 30, 34_700_000, tiers);
        AssertWritten("auction-goods-quote.bin", target => ShowAuctionGoodsQuotePlugIn.Write(target, 4, quotation));
    }

    /// <summary>
    /// The result of buying a listing is written as the fixture.
    /// </summary>
    [Test]
    public void BuyListingResultMatchesFixture()
    {
        var outcome = new AuctionBuyListingOutcome(AuctionHouseResult.Success, Id(0x00), 1_000_000_000, AuctionHouseDelivery.Inventory);
        AssertWritten("auction-buy-listing-result.bin", target => ShowAuctionBuyListingResultPlugIn.Write(target, 6, outcome));
    }

    /// <summary>
    /// The result of buying goods is written as the fixture.
    /// </summary>
    [Test]
    public void BuyGoodsResultMatchesFixture()
    {
        var outcome = new AuctionBuyGoodsOutcome(AuctionHouseResult.Success, JewelOfBless, 0, 18, 18_000_000, 10, 8);
        AssertWritten("auction-buy-goods-result.bin", target => ShowAuctionBuyGoodsResultPlugIn.Write(target, 7, outcome));
    }

    /// <summary>
    /// The result of creating a listing is written as the fixture.
    /// </summary>
    [Test]
    public void CreateListingResultMatchesFixture()
    {
        var outcome = new AuctionCreateListingOutcome(AuctionHouseResult.Success, Id(0x20), 18, 181_000);
        AssertWritten("auction-create-listing-result.bin", target => ShowAuctionCreateListingResultPlugIn.Write(target, 8, outcome));
    }

    /// <summary>
    /// The result of cancelling a listing is written as the fixture.
    /// </summary>
    [Test]
    public void CancelListingResultMatchesFixture()
    {
        var outcome = new AuctionCancelListingOutcome(AuctionHouseResult.Success, Id(0x20), 10, 8, 181_000);
        AssertWritten("auction-cancel-listing-result.bin", target => ShowAuctionCancelListingResultPlugIn.Write(target, 9, outcome));
    }

    /// <summary>
    /// The empty own listings are written as the fixture.
    /// </summary>
    [Test]
    public void OwnListingsEmptyMatchesFixture()
    {
        var listings = new AuctionOwnListingList(AuctionHouseResult.Success, []);
        AssertWritten("auction-own-listings-empty.bin", target => ShowAuctionOwnListingsPlugIn.Write(target, 10, listings, this._itemSerializer));
    }

    /// <summary>
    /// The own listings with an item listing and a goods listing are written as the fixture.
    /// </summary>
    [Test]
    public void OwnListingsMatchesFixture()
    {
        var listings = new AuctionOwnListingList(AuctionHouseResult.Success, [
            new AuctionOwnListingRow(Id(0x30), 2_147_483_647, 1, 1, 172_800, AuctionHouseListingKind.Item, Blade, 9, this._blade),
            new AuctionOwnListingRow(Id(0x20), 1_000_000, 18, 10, 43_200, AuctionHouseListingKind.Goods, JewelOfBless, 0, null),
        ]);
        AssertWritten("auction-own-listings.bin", target => ShowAuctionOwnListingsPlugIn.Write(target, 11, listings, this._itemSerializer));
    }

    /// <summary>
    /// The empty collection is written as the fixture.
    /// </summary>
    [Test]
    public void CollectionEmptyMatchesFixture()
    {
        var page = new AuctionCollectionPage(AuctionHouseResult.Success, 0, 0, []);
        AssertWritten("auction-collection-empty.bin", target => ShowAuctionCollectionPlugIn.Write(target, 12, page, this._itemSerializer));
    }

    /// <summary>
    /// The collection with a purchase, proceeds and expired goods is written as the fixture.
    /// </summary>
    [Test]
    public void CollectionMatchesFixture()
    {
        var page = new AuctionCollectionPage(AuctionHouseResult.Success, 0, 3, [
            new AuctionCollectionRow(Id(0x40), AuctionHouseCollectionEntryKind.Purchase, Armor, 7, 1, 1_000_000_000, 0, 0, this._ancientArmor),
            new AuctionCollectionRow(Id(0x50), AuctionHouseCollectionEntryKind.Proceeds, JewelOfBless, 0, 8, 8_000_000, 400_000, 7_600_000, null),
            new AuctionCollectionRow(Id(0x60), AuctionHouseCollectionEntryKind.Expired, JewelOfSoul, 0, 5, 0, 0, 0, null),
        ]);
        AssertWritten("auction-collection.bin", target => ShowAuctionCollectionPlugIn.Write(target, 13, page, this._itemSerializer));
    }

    /// <summary>
    /// The proceeds of an item listing carry the data of the display copy of the sold item, so that the client can name
    /// it as it looks ("Excellent Dragon Armor +9"); the collection is written as the fixture.
    /// </summary>
    [Test]
    public void CollectionProceedsItemMatchesFixture()
    {
        var page = new AuctionCollectionPage(AuctionHouseResult.Success, 0, 1, [
            new AuctionCollectionRow(Id(0x70), AuctionHouseCollectionEntryKind.Proceeds, (8 * 512) + 1, 9, 1, 26_800_000, 2_680_000, 24_120_000, this._excellentDragonArmor),
        ]);
        AssertWritten("auction-collection-proceeds-item.bin", target => ShowAuctionCollectionPlugIn.Write(target, 17, page, this._itemSerializer));
    }

    /// <summary>
    /// The partial claim result is written as the fixture.
    /// </summary>
    [Test]
    public void ClaimResultMatchesFixture()
    {
        var outcome = new AuctionClaimOutcome(AuctionHouseResult.Partial, 2, 9, 1_000_000_000, 1, AuctionHouseKeptBack.InventorySpace);
        AssertWritten("auction-claim-result.bin", target => ShowAuctionClaimResultPlugIn.Write(target, 14, outcome));
    }

    /// <summary>
    /// The sold notice is written as the fixture.
    /// </summary>
    [Test]
    public void NoticeSoldMatchesFixture()
    {
        var notice = new AuctionHouseNotice(AuctionHouseNoticeKind.Sold, JewelOfBless, 0, 8, 7_600_000, 2);
        AssertWritten("auction-notice-sold.bin", target => ShowAuctionNoticePlugIn.Write(target, notice, this._itemSerializer));
    }

    /// <summary>
    /// The sold notice of an item listing carries the sold item's data after the fixed part, so that the client can name
    /// it as it looks ("Excellent Dragon Armor +9").
    /// </summary>
    [Test]
    public void NoticeSoldItemMatchesFixture()
    {
        var notice = new AuctionHouseNotice(AuctionHouseNoticeKind.SoldToInventory, (8 * 512) + 1, 9, 1, 24_120_000, 0, this._excellentDragonArmor);
        AssertWritten("auction-notice-sold-item.bin", target => ShowAuctionNoticePlugIn.Write(target, notice, this._itemSerializer));
    }

    /// <summary>
    /// The deliveries waiting notice is written as the fixture.
    /// </summary>
    [Test]
    public void NoticeDeliveriesWaitingMatchesFixture()
    {
        var notice = new AuctionHouseNotice(AuctionHouseNoticeKind.DeliveriesWaiting, 0, 0, 0, 0, 3);
        AssertWritten("auction-notice-deliveries-waiting.bin", target => ShowAuctionNoticePlugIn.Write(target, notice, this._itemSerializer));
    }

    /// <summary>
    /// The notices of deliveries to the inventory are written with their kinds 4 and 5, in the layout of the fixtures.
    /// </summary>
    /// <param name="kind">The kind of the notice.</param>
    /// <param name="expectedKind">The expected kind byte.</param>
    [TestCase(AuctionHouseNoticeKind.SoldToInventory, 4)]
    [TestCase(AuctionHouseNoticeKind.ExpiredToInventory, 5)]
    public void NoticeToInventoryHasItsKind(AuctionHouseNoticeKind kind, byte expectedKind)
    {
        var notice = new AuctionHouseNotice(kind, JewelOfBless, 0, 8, 7_600_000, 2);
        var expected = AuctionHousePacketFixtureTest.ReadFixture("auction-notice-sold.bin");
        expected[4] = expectedKind;

        AssertWritten(expected, target => ShowAuctionNoticePlugIn.Write(target, notice, this._itemSerializer));
    }

    /// <summary>
    /// A broker state without price step restriction announces 0 price digits.
    /// </summary>
    [Test]
    public void StateOpenWithoutPriceStepsAnnouncesZeroDigits()
    {
        var rules = new AuctionHouseRules { PriceStepPrecision = 0 };
        var target = new byte[64];

        var length = ShowAuctionHouseStatePlugIn.Write(target, AuctionBrokerState.Create(rules, int.MaxValue, AuctionSellerSummary.None));

        Assert.That(length, Is.EqualTo(33));
        Assert.That(target[32], Is.EqualTo(0));
    }

    /// <summary>
    /// The sell check result of eligible goods is written as the fixture.
    /// </summary>
    [Test]
    public void SellCheckResultMatchesFixture()
    {
        var outcome = new AuctionSellCheckOutcome(AuctionHouseResult.Success, 12, AuctionHouseSellCheckReason.Eligible, AuctionHouseListingKind.Goods, JewelOfBless, 0, 18, 1_000_000, 3);
        AssertWritten("auction-sell-check-result.bin", target => ShowAuctionSellCheckResultPlugIn.Write(target, 16, outcome));
    }

    /// <summary>
    /// A seller name is cut to at most 10 bytes of UTF-8, on a character boundary.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="expected">The expected cut name.</param>
    [TestCase("Seller", "Seller")]
    [TestCase("Merchant10", "Merchant10")]
    [TestCase("Merchant10X", "Merchant10")]
    [TestCase("ÄÄÄÄÄÄ", "ÄÄÄÄÄ")]
    [TestCase("abcdefghi€", "abcdefghi")]
    [TestCase("", "")]
    public void SellerNameIsCutOnCharacterBoundary(string name, string expected)
    {
        var cut = AuctionHouseSerialization.CutName(name);

        Assert.That(cut, Is.EqualTo(expected));
        Assert.That(Encoding.UTF8.GetByteCount(cut), Is.LessThanOrEqualTo(AuctionHouseSerialization.NameLength));
    }

    /// <summary>
    /// A search row with a too long seller name is written with the cut name instead of failing.
    /// </summary>
    [Test]
    public void SearchResultCutsLongSellerName()
    {
        var page = new AuctionSearchPage(AuctionHouseResult.Success, 0, 1, [
            new AuctionListingRow(Id(0x00), 1, 1, "ÄÄÄÄÄÄÄÄ", false, false, this._blade),
        ]);
        var target = new byte[ShowAuctionSearchResultPlugIn.GetMaximumSize(page, this._itemSerializer)];

        var length = ShowAuctionSearchResultPlugIn.Write(target, 1, page, this._itemSerializer);

        Assert.That(length, Is.EqualTo(15 + 36 + BladeItemData.Length));
        Assert.That(target.AsSpan(15 + 24, 10).ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("ÄÄÄÄÄ")));
    }

    private static void AssertWritten(string fixtureName, WriteDelegate write)
        => AssertWritten(AuctionHousePacketFixtureTest.ReadFixture(fixtureName), write);

    private static void AssertWritten(byte[] expected, WriteDelegate write)
    {
        var target = new byte[1024];
        Array.Fill(target, (byte)0xCC);

        var length = write(target);

        Assert.That(target.AsSpan(0, length).ToArray(), Is.EqualTo(expected));
    }

    private static Guid Id(byte start) => new(Enumerable.Range(start, 16).Select(value => (byte)value).ToArray());

    private delegate int WriteDelegate(Span<byte> target);

    /// <summary>
    /// An item serializer which returns given item data per item.
    /// </summary>
    private sealed class ItemSerializerStub : IItemSerializer
    {
        private readonly IReadOnlyDictionary<Item, byte[]> _itemData;

        public ItemSerializerStub(IReadOnlyDictionary<Item, byte[]> itemData) => this._itemData = itemData;

        public int NeededSpace => 15;

        public int SerializeItem(Span<byte> target, Item item)
        {
            var data = this._itemData[item];
            data.CopyTo(target);
            return data.Length;
        }

        public Item DeserializeItem(Span<byte> source, GameConfiguration gameConfiguration, IContext persistenceContext) => throw new NotSupportedException();
    }
}
