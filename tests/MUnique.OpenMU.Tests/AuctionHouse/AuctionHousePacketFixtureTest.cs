// <copyright file="AuctionHousePacketFixtureTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.IO;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets.ServerToClient;

/// <summary>
/// Golden fixture tests of the packets of the auction house (<c>7B 50</c> to <c>7B 5C</c>). The checked-in bytes
/// were written from the byte layouts of the packet registry, independently of the generated packet code, and
/// the game client's tests parse the same bytes, so both sides agree on the wire format.
/// </summary>
[TestFixture]
public class AuctionHousePacketFixtureTest
{
    private const ushort JewelOfBless = (14 * 512) + 13;
    private const ushort JewelOfSoul = (14 * 512) + 14;
    private const ushort Blade = 5;
    private const ushort SocketSword = 26;
    private const ushort Armor = (8 * 512) + 9;

    /// <summary>
    /// The extended item data of a +9 blade with luck, skill, option level 3 and excellent options 0x09.
    /// </summary>
    private static readonly byte[] BladeItemData = [0x00, 0x05, 0x09, 0x2A, 0x0F, 0x03, 0x09];

    /// <summary>
    /// The extended item data of a +7 ancient armor with luck, option level 2 and ancient discriminator 1.
    /// </summary>
    private static readonly byte[] AncientArmorItemData = [0x80, 0x09, 0x07, 0x2D, 0x13, 0x02, 0x51];

    /// <summary>
    /// The extended item data of a +11 socket sword with skill, option level 1 and three sockets, one with a seed.
    /// </summary>
    private static readonly byte[] SocketSwordItemData = [0x00, 0x1A, 0x0B, 0x40, 0x85, 0x01, 0x03, 0x01, 0xFE, 0xFE];

    private static readonly string[] ExpectedFixtureFiles =
    [
        "auction-buy-goods-request.bin",
        "auction-buy-goods-result.bin",
        "auction-buy-listing-request.bin",
        "auction-buy-listing-result.bin",
        "auction-cancel-listing-request.bin",
        "auction-cancel-listing-result.bin",
        "auction-claim-request-all.bin",
        "auction-claim-request.bin",
        "auction-claim-result.bin",
        "auction-collection-empty.bin",
        "auction-collection-request.bin",
        "auction-collection.bin",
        "auction-create-listing-request.bin",
        "auction-create-listing-result.bin",
        "auction-goods-list-empty.bin",
        "auction-goods-list-request-all.bin",
        "auction-goods-list-request.bin",
        "auction-goods-list.bin",
        "auction-goods-quote-empty.bin",
        "auction-goods-quote-request.bin",
        "auction-goods-quote.bin",
        "auction-house-state-closed.bin",
        "auction-house-state-open.bin",
        "auction-notice-deliveries-waiting.bin",
        "auction-notice-sold.bin",
        "auction-own-listings-empty.bin",
        "auction-own-listings-request.bin",
        "auction-own-listings.bin",
        "auction-search-request-empty.bin",
        "auction-search-request.bin",
        "auction-search-result-empty.bin",
        "auction-search-result.bin",
        "auction-sell-check-request.bin",
        "auction-sell-check-result.bin",
    ];

    /// <summary>
    /// The fixture directory contains exactly the fixtures which are tested here.
    /// </summary>
    [Test]
    public void FixtureDirectoryContainsExactlyTheExpectedFiles()
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "AuctionHouse", "Fixtures");
        var files = Directory.GetFiles(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();

        Assert.That(files, Is.EqualTo(ExpectedFixtureFiles.Order(StringComparer.Ordinal).ToArray()));
    }

    /// <summary>
    /// The open <see cref="AuctionHouseState"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionHouseStateOpenMatchesFixture()
    {
        var bytes = new byte[AuctionHouseStateRef.Length];
        var packet = new AuctionHouseStateRef(bytes)
        {
            State = AuctionHouseWindowState.Open,
            PageSize = 20,
            MaxActiveListings = 30,
            ActiveListings = 3,
            PendingCollection = 2,
            ZenLimit = 2_147_483_647,
            MaxPrice = 2_000_000_000,
            PostingFeeFlat = 1_000,
            PostingFeeBasisPoints = 100,
            SaleFeeBasisPoints = 500,
            Flags = 0x01,
            DurationCount = 4,
        };
        new byte[] { 12, 24, 48, 72 }.CopyTo(packet.Durations);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-house-state-open.bin")));
    }

    /// <summary>
    /// The closed <see cref="AuctionHouseState"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionHouseStateClosedMatchesFixture()
    {
        var bytes = new byte[AuctionHouseStateRef.Length];
        _ = new AuctionHouseStateRef(bytes) { State = AuctionHouseWindowState.Closed };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-house-state-closed.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionSearchRequest"/> without filters and entries is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionSearchRequestEmptyMatchesFixture()
    {
        var bytes = new byte[AuctionSearchRequestRef.GetRequiredSize(0)];
        _ = new AuctionSearchRequestRef(bytes) { RequestId = 1, ClassLine = AuctionClassLine.Any, UpgradeMax = 15 };

        var fixture = ReadFixture("auction-search-request-empty.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionSearchRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(26));
        Assert.That(parsed.RequestId, Is.EqualTo(1));
        Assert.That(parsed.Page, Is.EqualTo(0));
        Assert.That(parsed.Sort, Is.EqualTo(AuctionSortOrder.PriceAscending));
        Assert.That(parsed.SlotMask, Is.EqualTo(0));
        Assert.That(parsed.GroupMask, Is.EqualTo(0));
        Assert.That(parsed.ClassLine, Is.EqualTo(AuctionClassLine.Any));
        Assert.That(parsed.Flags, Is.EqualTo(0));
        Assert.That(parsed.Family, Is.EqualTo(AuctionItemFamily.Any));
        Assert.That(parsed.UpgradeMin, Is.EqualTo(0));
        Assert.That(parsed.UpgradeMax, Is.EqualTo(15));
        Assert.That(parsed.ExcellentMask, Is.EqualTo(0));
        Assert.That(parsed.MinExcellentCount, Is.EqualTo(0));
        Assert.That(parsed.MinSockets, Is.EqualTo(0));
        Assert.That(parsed.MaxPrice, Is.EqualTo(0));
        Assert.That(parsed.EntryCount, Is.EqualTo(0));
    }

    /// <summary>
    /// The <see cref="AuctionSearchRequest"/> with every filter, two name entries and two ancient entries is
    /// serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionSearchRequestMatchesFixture()
    {
        var bytes = new byte[AuctionSearchRequestRef.GetRequiredSize(4)];
        var packet = new AuctionSearchRequestRef(bytes)
        {
            RequestId = 0x1234,
            Page = 2,
            Sort = AuctionSortOrder.PriceDescending,
            SlotMask = 0x0001,
            GroupMask = 0x0001,
            ClassLine = AuctionClassLine.DarkKnight,
            Flags = 0x07,
            Family = AuctionItemFamily.Excellent,
            UpgradeMin = 7,
            UpgradeMax = 15,
            ExcellentMask = 0x09,
            MinExcellentCount = 2,
            MinSockets = 0,
            MaxPrice = 2_147_483_647,
            EntryCount = 4,
        };
        SetEntry(packet[0], Blade, 0);
        SetEntry(packet[1], SocketSword, 0);
        SetEntry(packet[2], Armor, 1);
        SetEntry(packet[3], Armor, 2);

        var fixture = ReadFixture("auction-search-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionSearchRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(38));
        Assert.That(parsed.RequestId, Is.EqualTo(0x1234));
        Assert.That(parsed.Page, Is.EqualTo(2));
        Assert.That(parsed.Sort, Is.EqualTo(AuctionSortOrder.PriceDescending));
        Assert.That(parsed.SlotMask, Is.EqualTo(0x0001));
        Assert.That(parsed.GroupMask, Is.EqualTo(0x0001));
        Assert.That(parsed.ClassLine, Is.EqualTo(AuctionClassLine.DarkKnight));
        Assert.That(parsed.Flags, Is.EqualTo(0x07));
        Assert.That(parsed.Family, Is.EqualTo(AuctionItemFamily.Excellent));
        Assert.That(parsed.UpgradeMin, Is.EqualTo(7));
        Assert.That(parsed.UpgradeMax, Is.EqualTo(15));
        Assert.That(parsed.ExcellentMask, Is.EqualTo(0x09));
        Assert.That(parsed.MinExcellentCount, Is.EqualTo(2));
        Assert.That(parsed.MinSockets, Is.EqualTo(0));
        Assert.That(parsed.MaxPrice, Is.EqualTo(2_147_483_647));
        Assert.That(parsed.EntryCount, Is.EqualTo(4));
        var entries = new (ushort Type, byte Discriminator)[4];
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = parsed[i];
            entries[i] = (entry.Type, entry.Discriminator);
        }

        Assert.That(entries, Is.EqualTo(new (ushort, byte)[] { (Blade, 0), (SocketSword, 0), (Armor, 1), (Armor, 2) }));
    }

    /// <summary>
    /// The empty <see cref="AuctionSearchResult"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionSearchResultEmptyMatchesFixture()
    {
        var bytes = new byte[AuctionSearchResultRef.GetRequiredSize(0, 0)];
        _ = new AuctionSearchResultRef(bytes) { RequestId = 1, Result = AuctionResult.Success };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-search-result-empty.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionSearchResult"/> with two listings of different item data lengths is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionSearchResultMatchesFixture()
    {
        var firstSize = AuctionSearchResultRef.AuctionSearchListingRef.GetRequiredSize(BladeItemData.Length);
        var secondSize = AuctionSearchResultRef.AuctionSearchListingRef.GetRequiredSize(SocketSwordItemData.Length);
        var bytes = new byte[15 + firstSize + secondSize];
        _ = new AuctionSearchResultRef(bytes)
        {
            RequestId = 0x1234,
            Result = AuctionResult.Success,
            Page = 1,
            TotalCount = 45,
            Count = 2,
        };

        var first = new AuctionSearchResultRef.AuctionSearchListingRef(bytes.AsSpan(15, firstSize))
        {
            Price = 1_000_000_000,
            SecondsLeft = 86_400,
            Seller = "Seller",
            Flags = 0x01,
            ItemDataLength = (byte)BladeItemData.Length,
        };
        Id(0x00).CopyTo(first.ListingId);
        BladeItemData.CopyTo(first.ItemData);

        var second = new AuctionSearchResultRef.AuctionSearchListingRef(bytes.AsSpan(15 + firstSize, secondSize))
        {
            Price = 2_147_483_647,
            SecondsLeft = 3_600,
            Seller = "Merchant10",
            Flags = 0x03,
            ItemDataLength = (byte)SocketSwordItemData.Length,
        };
        Id(0x10).CopyTo(second.ListingId);
        SocketSwordItemData.CopyTo(second.ItemData);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-search-result.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionGoodsListRequest"/> for all goods is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsListRequestAllMatchesFixture()
    {
        var bytes = new byte[AuctionGoodsListRequestRef.GetRequiredSize(0)];
        _ = new AuctionGoodsListRequestRef(bytes) { RequestId = 2 };

        var fixture = ReadFixture("auction-goods-list-request-all.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionGoodsListRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(9));
        Assert.That(parsed.RequestId, Is.EqualTo(2));
        Assert.That(parsed.Page, Is.EqualTo(0));
        Assert.That(parsed.TypeCount, Is.EqualTo(0));
    }

    /// <summary>
    /// The <see cref="AuctionGoodsListRequest"/> limited to two item types is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsListRequestMatchesFixture()
    {
        var bytes = new byte[AuctionGoodsListRequestRef.GetRequiredSize(2)];
        var packet = new AuctionGoodsListRequestRef(bytes) { RequestId = 3, Page = 1, TypeCount = 2 };
        var firstType = packet[0];
        firstType.Type = JewelOfBless;
        var secondType = packet[1];
        secondType.Type = JewelOfSoul;

        var fixture = ReadFixture("auction-goods-list-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionGoodsListRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(13));
        Assert.That(parsed.RequestId, Is.EqualTo(3));
        Assert.That(parsed.Page, Is.EqualTo(1));
        Assert.That(parsed.TypeCount, Is.EqualTo(2));
        Assert.That(parsed[0].Type, Is.EqualTo(JewelOfBless));
        Assert.That(parsed[1].Type, Is.EqualTo(JewelOfSoul));
    }

    /// <summary>
    /// The empty <see cref="AuctionGoodsList"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsListEmptyMatchesFixture()
    {
        var bytes = new byte[AuctionGoodsListRef.GetRequiredSize(0)];
        _ = new AuctionGoodsListRef(bytes) { RequestId = 2, Result = AuctionResult.Success };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-goods-list-empty.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionGoodsList"/> with two goods is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsListMatchesFixture()
    {
        var bytes = new byte[AuctionGoodsListRef.GetRequiredSize(2)];
        var packet = new AuctionGoodsListRef(bytes)
        {
            RequestId = 3,
            Result = AuctionResult.Success,
            Page = 1,
            TotalCount = 27,
            Count = 2,
        };
        var first = packet[0];
        first.Type = JewelOfBless;
        first.Level = 0;
        first.LowestUnitPrice = 1_000_000;
        first.Available = 18;
        first.ListingCount = 3;
        var second = packet[1];
        second.Type = JewelOfSoul;
        second.Level = 0;
        second.LowestUnitPrice = 2_147_483_647;
        second.Available = 5;
        second.ListingCount = 1;

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-goods-list.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionGoodsQuoteRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsQuoteRequestMatchesFixture()
    {
        var bytes = new byte[AuctionGoodsQuoteRequestRef.Length];
        _ = new AuctionGoodsQuoteRequestRef(bytes) { RequestId = 4, Type = JewelOfBless, Level = 0, Quantity = 30 };

        var fixture = ReadFixture("auction-goods-quote-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionGoodsQuoteRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(13));
        Assert.That(parsed.RequestId, Is.EqualTo(4));
        Assert.That(parsed.Type, Is.EqualTo(JewelOfBless));
        Assert.That(parsed.Level, Is.EqualTo(0));
        Assert.That(parsed.Quantity, Is.EqualTo(30));
    }

    /// <summary>
    /// The <see cref="AuctionGoodsQuote"/> without stock is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsQuoteEmptyMatchesFixture()
    {
        var bytes = new byte[AuctionGoodsQuoteRef.GetRequiredSize(0)];
        _ = new AuctionGoodsQuoteRef(bytes)
        {
            RequestId = 5,
            Result = AuctionResult.InsufficientStock,
            Type = JewelOfSoul,
            Level = 0,
            RequestedQuantity = 5,
        };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-goods-quote-empty.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionGoodsQuote"/> with the maximum of eight price tiers is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionGoodsQuoteMatchesFixture()
    {
        var quantities = new uint[] { 5, 4, 4, 4, 4, 3, 3, 3 };
        var bytes = new byte[AuctionGoodsQuoteRef.GetRequiredSize(quantities.Length)];
        var packet = new AuctionGoodsQuoteRef(bytes)
        {
            RequestId = 4,
            Result = AuctionResult.Success,
            Type = JewelOfBless,
            Level = 0,
            RequestedQuantity = 30,
            FillableQuantity = 30,
            Total = 34_700_000,
            TierCount = (byte)quantities.Length,
        };
        for (var i = 0; i < quantities.Length; i++)
        {
            var tier = packet[i];
            tier.UnitPrice = (uint)(1_000_000 + (50_000 * i));
            tier.Quantity = quantities[i];
        }

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-goods-quote.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionBuyListingRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionBuyListingRequestMatchesFixture()
    {
        var bytes = new byte[AuctionBuyListingRequestRef.Length];
        var packet = new AuctionBuyListingRequestRef(bytes) { RequestId = 6, ReviewedPrice = 1_000_000_000 };
        Id(0x00).CopyTo(packet.ListingId);

        var fixture = ReadFixture("auction-buy-listing-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionBuyListingRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(26));
        Assert.That(parsed.RequestId, Is.EqualTo(6));
        Assert.That(parsed.ListingId.ToArray(), Is.EqualTo(Id(0x00)));
        Assert.That(parsed.ReviewedPrice, Is.EqualTo(1_000_000_000));
    }

    /// <summary>
    /// The <see cref="AuctionBuyListingResult"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionBuyListingResultMatchesFixture()
    {
        var bytes = new byte[AuctionBuyListingResultRef.Length];
        var packet = new AuctionBuyListingResultRef(bytes)
        {
            RequestId = 6,
            Result = AuctionResult.Success,
            Paid = 1_000_000_000,
            Delivery = AuctionDelivery.Inventory,
        };
        Id(0x00).CopyTo(packet.ListingId);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-buy-listing-result.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionBuyGoodsRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionBuyGoodsRequestMatchesFixture()
    {
        var bytes = new byte[AuctionBuyGoodsRequestRef.Length];
        _ = new AuctionBuyGoodsRequestRef(bytes) { RequestId = 7, Type = JewelOfBless, Level = 0, Quantity = 18, ReviewedTotal = 18_000_000 };

        var fixture = ReadFixture("auction-buy-goods-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionBuyGoodsRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(17));
        Assert.That(parsed.RequestId, Is.EqualTo(7));
        Assert.That(parsed.Type, Is.EqualTo(JewelOfBless));
        Assert.That(parsed.Level, Is.EqualTo(0));
        Assert.That(parsed.Quantity, Is.EqualTo(18));
        Assert.That(parsed.ReviewedTotal, Is.EqualTo(18_000_000));
    }

    /// <summary>
    /// The <see cref="AuctionBuyGoodsResult"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionBuyGoodsResultMatchesFixture()
    {
        var bytes = new byte[AuctionBuyGoodsResultRef.Length];
        _ = new AuctionBuyGoodsResultRef(bytes)
        {
            RequestId = 7,
            Result = AuctionResult.Success,
            Type = JewelOfBless,
            Level = 0,
            Quantity = 18,
            Paid = 18_000_000,
            Delivered = 10,
            Kept = 8,
        };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-buy-goods-result.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionCreateListingRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCreateListingRequestMatchesFixture()
    {
        var bytes = new byte[AuctionCreateListingRequestRef.Length];
        _ = new AuctionCreateListingRequestRef(bytes)
        {
            RequestId = 8,
            InventorySlot = 12,
            Quantity = 18,
            UnitPrice = 1_000_000,
            DurationHours = 24,
            ExpectedPostingFee = 181_000,
        };

        var fixture = ReadFixture("auction-create-listing-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionCreateListingRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(20));
        Assert.That(parsed.RequestId, Is.EqualTo(8));
        Assert.That(parsed.InventorySlot, Is.EqualTo(12));
        Assert.That(parsed.Quantity, Is.EqualTo(18));
        Assert.That(parsed.UnitPrice, Is.EqualTo(1_000_000));
        Assert.That(parsed.DurationHours, Is.EqualTo(24));
        Assert.That(parsed.ExpectedPostingFee, Is.EqualTo(181_000));
    }

    /// <summary>
    /// The <see cref="AuctionCreateListingResult"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCreateListingResultMatchesFixture()
    {
        var bytes = new byte[AuctionCreateListingResultRef.Length];
        var packet = new AuctionCreateListingResultRef(bytes)
        {
            RequestId = 8,
            Result = AuctionResult.Success,
            Quantity = 18,
            PostingFee = 181_000,
        };
        Id(0x20).CopyTo(packet.ListingId);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-create-listing-result.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionCancelListingRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCancelListingRequestMatchesFixture()
    {
        var bytes = new byte[AuctionCancelListingRequestRef.Length];
        var packet = new AuctionCancelListingRequestRef(bytes) { RequestId = 9 };
        Id(0x20).CopyTo(packet.ListingId);

        var fixture = ReadFixture("auction-cancel-listing-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionCancelListingRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(22));
        Assert.That(parsed.RequestId, Is.EqualTo(9));
        Assert.That(parsed.ListingId.ToArray(), Is.EqualTo(Id(0x20)));
    }

    /// <summary>
    /// The <see cref="AuctionCancelListingResult"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCancelListingResultMatchesFixture()
    {
        var bytes = new byte[AuctionCancelListingResultRef.Length];
        var packet = new AuctionCancelListingResultRef(bytes)
        {
            RequestId = 9,
            Result = AuctionResult.Success,
            ToInventory = 10,
            ToCollection = 8,
            RefundedFee = 181_000,
        };
        Id(0x20).CopyTo(packet.ListingId);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-cancel-listing-result.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionOwnListingsRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionOwnListingsRequestMatchesFixture()
    {
        var bytes = new byte[AuctionOwnListingsRequestRef.Length];
        _ = new AuctionOwnListingsRequestRef(bytes) { RequestId = 10 };

        var fixture = ReadFixture("auction-own-listings-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionOwnListingsRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(6));
        Assert.That(parsed.RequestId, Is.EqualTo(10));
    }

    /// <summary>
    /// The empty <see cref="AuctionOwnListings"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionOwnListingsEmptyMatchesFixture()
    {
        var bytes = new byte[AuctionOwnListingsRef.GetRequiredSize(0, 0)];
        _ = new AuctionOwnListingsRef(bytes) { RequestId = 10, Result = AuctionResult.Success };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-own-listings-empty.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionOwnListings"/> with an item listing and a goods listing is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionOwnListingsMatchesFixture()
    {
        var firstSize = AuctionOwnListingsRef.AuctionOwnListingRef.GetRequiredSize(BladeItemData.Length);
        var secondSize = AuctionOwnListingsRef.AuctionOwnListingRef.GetRequiredSize(0);
        var bytes = new byte[9 + firstSize + secondSize];
        _ = new AuctionOwnListingsRef(bytes) { RequestId = 11, Result = AuctionResult.Success, Count = 2 };

        var first = new AuctionOwnListingsRef.AuctionOwnListingRef(bytes.AsSpan(9, firstSize))
        {
            UnitPrice = 2_147_483_647,
            Listed = 1,
            Remaining = 1,
            SecondsLeft = 172_800,
            Kind = AuctionListingKind.Item,
            Type = Blade,
            Level = 9,
            ItemDataLength = (byte)BladeItemData.Length,
        };
        Id(0x30).CopyTo(first.ListingId);
        BladeItemData.CopyTo(first.ItemData);

        var second = new AuctionOwnListingsRef.AuctionOwnListingRef(bytes.AsSpan(9 + firstSize, secondSize))
        {
            UnitPrice = 1_000_000,
            Listed = 18,
            Remaining = 10,
            SecondsLeft = 43_200,
            Kind = AuctionListingKind.Goods,
            Type = JewelOfBless,
            Level = 0,
            ItemDataLength = 0,
        };
        Id(0x20).CopyTo(second.ListingId);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-own-listings.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionCollectionRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCollectionRequestMatchesFixture()
    {
        var bytes = new byte[AuctionCollectionRequestRef.Length];
        _ = new AuctionCollectionRequestRef(bytes) { RequestId = 12, Page = 1 };

        var fixture = ReadFixture("auction-collection-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionCollectionRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(8));
        Assert.That(parsed.RequestId, Is.EqualTo(12));
        Assert.That(parsed.Page, Is.EqualTo(1));
    }

    /// <summary>
    /// The empty <see cref="AuctionCollection"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCollectionEmptyMatchesFixture()
    {
        var bytes = new byte[AuctionCollectionRef.GetRequiredSize(0, 0)];
        _ = new AuctionCollectionRef(bytes) { RequestId = 12, Result = AuctionResult.Success };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-collection-empty.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionCollection"/> with a purchase, proceeds and expired goods is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionCollectionMatchesFixture()
    {
        var firstSize = AuctionCollectionRef.AuctionCollectionEntryRef.GetRequiredSize(AncientArmorItemData.Length);
        var goodsSize = AuctionCollectionRef.AuctionCollectionEntryRef.GetRequiredSize(0);
        var bytes = new byte[13 + firstSize + (2 * goodsSize)];
        _ = new AuctionCollectionRef(bytes)
        {
            RequestId = 13,
            Result = AuctionResult.Success,
            Page = 0,
            TotalCount = 3,
            Count = 3,
        };

        var purchase = new AuctionCollectionRef.AuctionCollectionEntryRef(bytes.AsSpan(13, firstSize))
        {
            Kind = AuctionCollectionEntryKind.Purchase,
            Type = Armor,
            Level = 7,
            Quantity = 1,
            Gross = 1_000_000_000,
            Fee = 0,
            Net = 0,
            ItemDataLength = (byte)AncientArmorItemData.Length,
        };
        Id(0x40).CopyTo(purchase.EntryId);
        AncientArmorItemData.CopyTo(purchase.ItemData);

        var proceeds = new AuctionCollectionRef.AuctionCollectionEntryRef(bytes.AsSpan(13 + firstSize, goodsSize))
        {
            Kind = AuctionCollectionEntryKind.Proceeds,
            Type = JewelOfBless,
            Level = 0,
            Quantity = 8,
            Gross = 8_000_000,
            Fee = 400_000,
            Net = 7_600_000,
            ItemDataLength = 0,
        };
        Id(0x50).CopyTo(proceeds.EntryId);

        var expired = new AuctionCollectionRef.AuctionCollectionEntryRef(bytes.AsSpan(13 + firstSize + goodsSize, goodsSize))
        {
            Kind = AuctionCollectionEntryKind.Expired,
            Type = JewelOfSoul,
            Level = 0,
            Quantity = 5,
            ItemDataLength = 0,
        };
        Id(0x60).CopyTo(expired.EntryId);

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-collection.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionClaimRequest"/> for every entry is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionClaimRequestAllMatchesFixture()
    {
        var bytes = new byte[AuctionClaimRequestRef.Length];
        _ = new AuctionClaimRequestRef(bytes) { RequestId = 14 };

        var fixture = ReadFixture("auction-claim-request-all.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionClaimRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(22));
        Assert.That(parsed.RequestId, Is.EqualTo(14));
        Assert.That(parsed.EntryId.ToArray(), Is.EqualTo(new byte[16]));
    }

    /// <summary>
    /// The <see cref="AuctionClaimRequest"/> for one entry is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionClaimRequestMatchesFixture()
    {
        var bytes = new byte[AuctionClaimRequestRef.Length];
        var packet = new AuctionClaimRequestRef(bytes) { RequestId = 15 };
        Id(0x50).CopyTo(packet.EntryId);

        var fixture = ReadFixture("auction-claim-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionClaimRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(22));
        Assert.That(parsed.RequestId, Is.EqualTo(15));
        Assert.That(parsed.EntryId.ToArray(), Is.EqualTo(Id(0x50)));
    }

    /// <summary>
    /// The partial <see cref="AuctionClaimResult"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionClaimResultMatchesFixture()
    {
        var bytes = new byte[AuctionClaimResultRef.Length];
        _ = new AuctionClaimResultRef(bytes)
        {
            RequestId = 14,
            Result = AuctionResult.Partial,
            CompletedEntries = 2,
            ItemsMoved = 9,
            ZenCollected = 1_000_000_000,
            RemainingEntries = 1,
            KeptBack = 0x01,
        };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-claim-result.bin")));
    }

    /// <summary>
    /// The sold <see cref="AuctionNotice"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionNoticeSoldMatchesFixture()
    {
        var bytes = new byte[AuctionNoticeRef.Length];
        _ = new AuctionNoticeRef(bytes)
        {
            Kind = AuctionNoticeKind.Sold,
            Type = JewelOfBless,
            Level = 0,
            Quantity = 8,
            Net = 7_600_000,
            PendingCollection = 2,
        };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-notice-sold.bin")));
    }

    /// <summary>
    /// The deliveries waiting <see cref="AuctionNotice"/> is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionNoticeDeliveriesWaitingMatchesFixture()
    {
        var bytes = new byte[AuctionNoticeRef.Length];
        _ = new AuctionNoticeRef(bytes) { Kind = AuctionNoticeKind.DeliveriesWaiting, PendingCollection = 3 };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-notice-deliveries-waiting.bin")));
    }

    /// <summary>
    /// The <see cref="AuctionSellCheckRequest"/> is serialized to and parsed from the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionSellCheckRequestMatchesFixture()
    {
        var bytes = new byte[AuctionSellCheckRequestRef.Length];
        _ = new AuctionSellCheckRequestRef(bytes) { RequestId = 16, InventorySlot = 12 };

        var fixture = ReadFixture("auction-sell-check-request.bin");
        Assert.That(bytes, Is.EqualTo(fixture));

        AuctionSellCheckRequestRef parsed = fixture.AsSpan();
        Assert.That(parsed.Header.Length, Is.EqualTo(7));
        Assert.That(parsed.RequestId, Is.EqualTo(16));
        Assert.That(parsed.InventorySlot, Is.EqualTo(12));
    }

    /// <summary>
    /// The <see cref="AuctionSellCheckResult"/> of eligible goods is serialized to the fixture bytes.
    /// </summary>
    [Test]
    public void AuctionSellCheckResultMatchesFixture()
    {
        var bytes = new byte[AuctionSellCheckResultRef.Length];
        _ = new AuctionSellCheckResultRef(bytes)
        {
            RequestId = 16,
            Result = AuctionResult.Success,
            InventorySlot = 12,
            Reason = AuctionSellCheckReason.Eligible,
            Kind = AuctionListingKind.Goods,
            Type = JewelOfBless,
            Level = 0,
            Available = 18,
            LowestUnitPrice = 1_000_000,
            ActiveListings = 3,
        };

        Assert.That(bytes, Is.EqualTo(ReadFixture("auction-sell-check-result.bin")));
    }

    /// <summary>
    /// Reads a golden fixture of the auction house packets.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The bytes of the fixture.</returns>
    internal static byte[] ReadFixture(string fileName)
    {
        return File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "AuctionHouse", "Fixtures", fileName));
    }

    /// <summary>
    /// Gets the 16 byte id <c>start</c>, <c>start + 1</c>, …, <c>start + 15</c>.
    /// </summary>
    /// <param name="start">The first byte of the id.</param>
    /// <returns>The id.</returns>
    private static byte[] Id(byte start)
    {
        return Enumerable.Range(start, 16).Select(value => (byte)value).ToArray();
    }

    private static void SetEntry(AuctionSearchRequestRef.AuctionSearchEntryRef entry, ushort type, byte discriminator)
    {
        entry.Type = type;
        entry.Discriminator = discriminator;
    }
}
