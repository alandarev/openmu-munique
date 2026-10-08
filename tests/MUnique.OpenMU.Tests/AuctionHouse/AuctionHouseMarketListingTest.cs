// <copyright file="AuctionHouseMarketListingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests for the sell check, creating and cancelling listings of the <see cref="AuctionHouseMarket"/>.
/// </summary>
[TestFixture]
public class AuctionHouseMarketListingTest
{
    private const byte Slot = AuctionHouseMarketTestHelper.FirstInventorySlot;

    private AuctionHouseMarketTestHelper _helper = null!;
    private AuctionHouseMarket _market = null!;
    private Player _seller = null!;

    /// <summary>
    /// Creates a market and a seller with 100,000,000 Zen.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._helper = new AuctionHouseMarketTestHelper();
        var gameContext = this._helper.CreateGameContext();
        this._market = AuctionHouseMarkets.Get(gameContext);
        this._seller = await this._helper.CreatePlayerAsync(gameContext, "seller", 100_000_000).ConfigureAwait(false);
    }

    /// <summary>
    /// An empty slot is answered with the reason "no item".
    /// </summary>
    [Test]
    public async Task SellCheckOfEmptySlotSaysNoItemAsync()
    {
        var outcome = await this._market.CheckSellAsync(this._seller, new AuctionSellCheckQuery(Slot)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Reason, Is.EqualTo(AuctionHouseSellCheckReason.NoItem));
    }

    /// <summary>
    /// Bound and untradable items are not tradable; stackable consumables and equipped items are not accepted.
    /// </summary>
    /// <param name="itemName">The name of the item of the helper.</param>
    /// <param name="slot">The inventory slot.</param>
    /// <param name="expectedReason">The expected reason.</param>
    [TestCase(nameof(AuctionHouseMarketTestHelper.BoundItem), Slot, AuctionHouseSellCheckReason.NotTradable)]
    [TestCase(nameof(AuctionHouseMarketTestHelper.UntradableItem), Slot, AuctionHouseSellCheckReason.NotTradable)]
    [TestCase(nameof(AuctionHouseMarketTestHelper.SmallHealingPotion), Slot, AuctionHouseSellCheckReason.NotAccepted)]
    [TestCase(nameof(AuctionHouseMarketTestHelper.Kris), (byte)0, AuctionHouseSellCheckReason.NotAccepted)]
    public async Task SellCheckRefusesIneligibleItemsAsync(string itemName, byte slot, AuctionHouseSellCheckReason expectedReason)
    {
        var definition = (ItemDefinition)typeof(AuctionHouseMarketTestHelper).GetProperty(itemName)!.GetValue(this._helper)!;
        await this._helper.AddItemAsync(this._seller, definition, slot).ConfigureAwait(false);

        var check = await this._market.CheckSellAsync(this._seller, new AuctionSellCheckQuery(slot)).ConfigureAwait(false);
        var create = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(slot, 1, 10_000, 48, 1_000)).ConfigureAwait(false);

        Assert.That(check.Reason, Is.EqualTo(expectedReason));
        Assert.That(create.Result, Is.EqualTo(AuctionHouseResult.NotEligible));
        Assert.That(this._seller.Inventory!.GetItem(slot), Is.Not.Null);
        Assert.That(this._seller.Money, Is.EqualTo(100_000_000));
    }

    /// <summary>
    /// A stackable item which is configured as goods is not accepted.
    /// </summary>
    [Test]
    public async Task StackableGoodsAreNotAcceptedAsync()
    {
        this._helper.Rules.Goods.Add(new AuctionGoodsItem { Group = 14, Number = 0, Name = "Small Healing Potion" });
        await this._helper.AddItemAsync(this._seller, this._helper.SmallHealingPotion, Slot).ConfigureAwait(false);

        var check = await this._market.CheckSellAsync(this._seller, new AuctionSellCheckQuery(Slot)).ConfigureAwait(false);

        Assert.That(check.Reason, Is.EqualTo(AuctionHouseSellCheckReason.NotAccepted));
        Assert.That(check.Kind, Is.EqualTo(AuctionHouseListingKind.Goods));
    }

    /// <summary>
    /// Loose jewels are goods: the check counts the identical loose units and names the lowest price of other sellers.
    /// </summary>
    [Test]
    public async Task SellCheckOfJewelsCountsUnitsAndLowestPriceAsync()
    {
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 18).ConfigureAwait(false);
        await this._helper.AddItemAsync(this._seller, this._helper.JewelOfBless, 40, 1).ConfigureAwait(false);
        var other = await this._helper.CreatePlayerAsync(this._seller.GameContext, "other", 10_000_000).ConfigureAwait(false);
        await this._helper.AddItemsAsync(other, this._helper.JewelOfBless, 2).ConfigureAwait(false);
        await this._market.CreateListingAsync(other, new AuctionCreateListingOrder(Slot, 2, 990_000, 48, 0)).ConfigureAwait(false);

        var check = await this._market.CheckSellAsync(this._seller, new AuctionSellCheckQuery(Slot + 3)).ConfigureAwait(false);

        Assert.That(check.Reason, Is.EqualTo(AuctionHouseSellCheckReason.Eligible));
        Assert.That(check.Kind, Is.EqualTo(AuctionHouseListingKind.Goods));
        Assert.That(check.ItemType, Is.EqualTo(this._helper.JewelOfBlessType));
        Assert.That(check.Available, Is.EqualTo(18));
        Assert.That(check.LowestUnitPrice, Is.EqualTo(990_000));
        Assert.That(check.ActiveListings, Is.Zero);
    }

    /// <summary>
    /// Listing goods takes the units starting at the slot, charges the posting fee and puts the listing into the index.
    /// </summary>
    [Test]
    public async Task CreatingGoodsListingEscrowsUnitsAndChargesFeeAsync()
    {
        var jewels = await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 18).ConfigureAwait(false);

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot + 2, 10, 3_000_000, 48, 1_500_000)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.Quantity, Is.EqualTo(10));
        Assert.That(outcome.PostingFee, Is.EqualTo(1_500_000));
        Assert.That(this._seller.Money, Is.EqualTo(98_500_000));
        Assert.That(this._seller.Inventory!.Items.Count(item => item.Definition == this._helper.JewelOfBless), Is.EqualTo(8));
        Assert.That(this._seller.Inventory.GetItem(Slot + 2), Is.Null, "the slot of the request is taken first");
        Assert.That(this._seller.Inventory.GetItem(Slot), Is.SameAs(jewels[0]), "units before the slot are taken last");
        var indexed = this._market.Index.Get(outcome.ListingId);
        Assert.That(indexed?.RemainingQuantity, Is.EqualTo(10));
        Assert.That(indexed?.UnitPrice, Is.EqualTo(3_000_000));
        var listing = await this._helper.GetListingAsync(outcome.ListingId).ConfigureAwait(false);
        Assert.That(listing!.Escrow!.Items, Has.Count.EqualTo(10));
        Assert.That(listing.ExpiresAt - listing.CreatedAt, Is.EqualTo(TimeSpan.FromHours(48)));
        Assert.That(listing.SellerName, Is.EqualTo("seller"));
    }

    /// <summary>
    /// Every refusal of creating a listing changes nothing.
    /// </summary>
    /// <param name="quantity">The quantity.</param>
    /// <param name="unitPrice">The unit price.</param>
    /// <param name="money">The Zen of the seller.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(19u, 3_000_000u, 100_000_000, AuctionHouseResult.InvalidRequest)]
    [TestCase(0u, 3_000_000u, 100_000_000, AuctionHouseResult.InvalidRequest)]
    [TestCase(10u, 3_001_000u, 100_000_000, AuctionHouseResult.PriceNotOnStep)]
    [TestCase(10u, 3_000_000u, 1_499_999, AuctionHouseResult.InsufficientZen)]
    [TestCase(10u, 300_000_000u, 2_000_000_000, AuctionHouseResult.PriceOutOfRange)]
    public async Task CreatingGoodsListingRefusalsChangeNothingAsync(uint quantity, uint unitPrice, int money, AuctionHouseResult expected)
    {
        this._seller.SelectedCharacter!.Inventory!.Money = money;
        var moneyBefore = this._seller.Money;
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 18).ConfigureAwait(false);

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, quantity, unitPrice, 48, 0)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(expected));
        Assert.That(this._seller.Money, Is.EqualTo(moneyBefore));
        Assert.That(this._seller.Inventory!.Items.Count(), Is.EqualTo(18));
        Assert.That(this._market.Index.Count, Is.Zero);
    }

    /// <summary>
    /// An item listing has the quantity 1.
    /// </summary>
    [Test]
    public async Task ItemListingWithQuantityAboveOneIsInvalidAsync()
    {
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 2, 10_000, 48, 0)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.InvalidRequest));
    }

    /// <summary>
    /// With the default rules, a goods unit price must be a multiple of its price step (about 1 %); a price off a step is
    /// refused with <see cref="AuctionHouseResult.PriceNotOnStep"/>, never rounded, and changes nothing.
    /// </summary>
    /// <param name="unitPrice">The unit price.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(594_000u, AuctionHouseResult.PriceNotOnStep)]
    [TestCase(595_000u, AuctionHouseResult.Success)]
    public async Task GoodsListingPriceStepsWithDefaultRulesAsync(uint unitPrice, AuctionHouseResult expected)
    {
        Assert.That(this._helper.Rules.PriceStepPrecision, Is.EqualTo(2));
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 2).ConfigureAwait(false);

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 2, unitPrice, 48, 0)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(expected));
        if (expected == AuctionHouseResult.Success)
        {
            Assert.That(this._market.Index.Get(outcome.ListingId)?.UnitPrice, Is.EqualTo(unitPrice));
        }
        else
        {
            Assert.That(this._seller.Money, Is.EqualTo(100_000_000));
            Assert.That(this._seller.Inventory!.Items.Count(item => item.Definition == this._helper.JewelOfBless), Is.EqualTo(2));
            Assert.That(this._market.Index.Count, Is.Zero);
        }
    }

    /// <summary>
    /// With the default rules, an item listing price off a step is refused with
    /// <see cref="AuctionHouseResult.PriceNotOnStep"/>: no fee is charged and the item stays in the inventory.
    /// </summary>
    [Test]
    public async Task ItemListingOffPriceStepIsRefusedAndChangesNothingAsync()
    {
        var item = await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, 12_550_000, 48, 0)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.PriceNotOnStep));
        Assert.That(this._seller.Money, Is.EqualTo(100_000_000));
        Assert.That(this._seller.Inventory!.GetItem(Slot), Is.SameAs(item));
        Assert.That(this._market.Index.Count, Is.Zero);
    }

    /// <summary>
    /// With the default rules, an item listing price on a step is accepted as it is.
    /// </summary>
    [Test]
    public async Task ItemListingOnPriceStepIsAcceptedAsync()
    {
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, 12_500_000, 48, 0)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(this._market.Index.Get(outcome.ListingId)?.Item, Is.Not.Null);
        Assert.That(this._market.Index.Get(outcome.ListingId)?.UnitPrice, Is.EqualTo(12_500_000));
        Assert.That(this._seller.Inventory!.GetItem(Slot), Is.Null);
    }

    /// <summary>
    /// A price step precision of 0 means no restriction, for goods and item listings alike.
    /// </summary>
    [Test]
    public async Task ZeroPriceStepPrecisionAllowsAnyPriceAsync()
    {
        this._helper.Rules.PriceStepPrecision = 0;
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 2).ConfigureAwait(false);
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot + 2).ConfigureAwait(false);

        var goods = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 2, 595_001, 48, 0)).ConfigureAwait(false);
        var item = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot + 2, 1, 12_345_678, 48, 0)).ConfigureAwait(false);

        Assert.That(goods.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(item.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(this._market.Index.Get(goods.ListingId)?.UnitPrice, Is.EqualTo(595_001));
        Assert.That(this._market.Index.Get(item.ListingId)?.UnitPrice, Is.EqualTo(12_345_678));
    }

    /// <summary>
    /// A seller can't have more active listings than the rules allow.
    /// </summary>
    [Test]
    public async Task ListingLimitIsEnforcedAsync()
    {
        this._helper.Rules.MaximumActiveListings = 2;
        await this._helper.AddItemsAsync(this._seller, this._helper.Kris, 3, Slot).ConfigureAwait(false);
        for (byte i = 0; i < 2; i++)
        {
            var created = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder((byte)(Slot + i), 1, 10_000, 48, 0)).ConfigureAwait(false);
            Assert.That(created.Result, Is.EqualTo(AuctionHouseResult.Success));
        }

        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot + 2, 1, 10_000, 48, 0)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.ListingLimitReached));
        Assert.That(this._seller.Inventory!.GetItem(Slot + 2), Is.Not.Null);
    }

    /// <summary>
    /// An excellent, ancient socket item keeps its complete state through the escrow and back to the inventory.
    /// </summary>
    [Test]
    public async Task EscrowKeepsTheCompleteItemStateAsync()
    {
        var item = await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot, 11).ConfigureAwait(false);
        var context = this._seller.PersistenceContext;
        var optionDefinition = context.CreateNew<IncreasableItemOption>();
        var excellent = context.CreateNew<ItemOptionLink>();
        excellent.ItemOption = optionDefinition;
        excellent.Level = 2;
        item.ItemOptions.Add(excellent);
        var setGroup = context.CreateNew<ItemSetGroup>();
        var ancient = context.CreateNew<ItemOfItemSet>();
        ancient.ItemSetGroup = setGroup;
        item.ItemSetGroups.Add(ancient);
        item.SocketCount = 3;
        item.HasSkill = true;
        item.Durability = 17;

        var created = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, 50_000_000, 48, 0)).ConfigureAwait(false);
        var listing = await this._helper.GetListingAsync(created.ListingId).ConfigureAwait(false);
        Assert.That(listing!.Escrow!.Items, Is.EqualTo(new[] { item }));
        var cancelled = await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);

        Assert.That(cancelled.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(cancelled.ToInventory, Is.EqualTo(1));
        var returned = this._seller.Inventory!.Items.Single();
        Assert.That(returned, Is.SameAs(item));
        Assert.That(returned.Level, Is.EqualTo(11));
        Assert.That(returned.Durability, Is.EqualTo(17));
        Assert.That(returned.SocketCount, Is.EqualTo(3));
        Assert.That(returned.HasSkill, Is.True);
        Assert.That(returned.ItemOptions.Single().ItemOption, Is.SameAs(optionDefinition));
        Assert.That(returned.ItemOptions.Single().Level, Is.EqualTo(2));
        Assert.That(returned.ItemSetGroups.Single().ItemSetGroup, Is.SameAs(setGroup));
    }

    /// <summary>
    /// Cancelling returns the remaining units to the inventory, the posting fee isn't refunded by default, and the
    /// listing leaves the index.
    /// </summary>
    [Test]
    public async Task CancellingReturnsUnitsWithoutRefundAsync()
    {
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 10).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 10, 3_000_000, 48, 0)).ConfigureAwait(false);

        var outcome = await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.ToInventory, Is.EqualTo(10));
        Assert.That(outcome.ToCollection, Is.Zero);
        Assert.That(outcome.RefundedFee, Is.Zero);
        Assert.That(this._seller.Inventory!.Items.Count(), Is.EqualTo(10));
        Assert.That(this._seller.Money, Is.EqualTo(98_500_000));
        Assert.That(this._market.Index.Get(created.ListingId), Is.Null);
        Assert.That((await this._helper.GetListingAsync(created.ListingId).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Cancelled));
        Assert.That(await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false), Is.Empty);
    }

    /// <summary>
    /// With the refund rule, cancelling refunds the posting fee.
    /// </summary>
    [Test]
    public async Task CancellingRefundsFeeWhenRulesSayAsync()
    {
        this._helper.Rules.RefundPostingFeeOnCancel = true;
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, 1_000_000, 48, 0)).ConfigureAwait(false);

        var outcome = await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);

        Assert.That(outcome.RefundedFee, Is.EqualTo(50_000));
        Assert.That(this._seller.Money, Is.EqualTo(100_000_000));
    }

    /// <summary>
    /// Units which don't fit into the inventory are kept in a "cancelled" collection entry.
    /// </summary>
    [Test]
    public async Task CancellingWithFullInventoryKeepsUnitsInCollectionAsync()
    {
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, 4).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 4, 3_000_000, 48, 0)).ConfigureAwait(false);
        await this._helper.FillInventoryAsync(this._seller).ConfigureAwait(false);

        var outcome = await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.ToInventory, Is.Zero);
        Assert.That(outcome.ToCollection, Is.EqualTo(4));
        var entry = (await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false)).Single();
        Assert.That(entry.Kind, Is.EqualTo(AuctionHouseCollectionEntryKind.Cancelled));
        Assert.That(entry.Quantity, Is.EqualTo(4));
        Assert.That(entry.Escrow!.Items, Has.Count.EqualTo(4));
        Assert.That(entry.ListingId, Is.EqualTo(created.ListingId));
    }

    /// <summary>
    /// Cancelling an unknown listing, the listing of another seller or an inactive listing is answered "not found".
    /// </summary>
    [Test]
    public async Task CancellingForeignOrInactiveListingIsNotFoundAsync()
    {
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, 1_000_000, 48, 0)).ConfigureAwait(false);
        var other = await this._helper.CreatePlayerAsync(this._seller.GameContext, "other").ConfigureAwait(false);

        var foreign = await this._market.CancelListingAsync(other, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);
        var unknown = await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(Guid.NewGuid())).ConfigureAwait(false);
        await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);
        var inactive = await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(created.ListingId)).ConfigureAwait(false);

        Assert.That(foreign.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(unknown.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(inactive.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(this._seller.Inventory!.Items.Count(), Is.EqualTo(1));
    }
}
