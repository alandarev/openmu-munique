// <copyright file="AuctionHouseMarketReadingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests for the reading requests of the <see cref="AuctionHouseMarket"/>: search with each criterion, goods list,
/// quote, own listings, collection and seller summary.
/// </summary>
[TestFixture]
public class AuctionHouseMarketReadingTest
{
    private const byte Slot = AuctionHouseMarketTestHelper.FirstInventorySlot;

    private static readonly AuctionSearchQuery AnyItem = new(0, AuctionHouseSortOrder.PriceAscending, AuctionHouseEquipmentSlots.None, 0, AuctionHouseClassLine.Any, AuctionHouseSearchOptions.None, AuctionHouseItemFamily.Any, 0, 15, 0, 0, 0, 0, [], []);

    private AuctionHouseMarketTestHelper _helper = null!;
    private IGameContext _gameContext = null!;
    private AuctionHouseMarket _market = null!;
    private Player _seller = null!;
    private Player _buyer = null!;
    private IContext _context = null!;
    private ItemOptionDefinition _excellentOptions = null!;
    private IncreasableItemOption _luck = null!;
    private CharacterClass _knight = null!;
    private Dictionary<string, Guid> _listings = null!;

    /// <summary>
    /// Creates a market with a seller of several item listings and a buyer.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._helper = new AuctionHouseMarketTestHelper();
        this._gameContext = this._helper.CreateGameContext();
        this._market = AuctionHouseMarkets.Get(this._gameContext);
        this._seller = await this._helper.CreatePlayerAsync(this._gameContext, "seller", 100_000_000).ConfigureAwait(false);
        this._buyer = await this._helper.CreatePlayerAsync(this._gameContext, "buyer", 100_000_000).ConfigureAwait(false);
        this._context = this._helper.ContextProvider.CreateNewContext();
        this._excellentOptions = this._context.CreateNew<ItemOptionDefinition>();
        for (var number = 1; number <= 6; number++)
        {
            var option = this._context.CreateNew<IncreasableItemOption>();
            option.OptionType = ItemOptionTypes.Excellent;
            option.Number = number;
            this._excellentOptions.PossibleOptions.Add(option);
        }

        this._luck = this._context.CreateNew<IncreasableItemOption>();
        this._luck.OptionType = ItemOptionTypes.Luck;
        this._knight = this._context.CreateNew<CharacterClass>();
        this._knight.Number = 4; // Dark Knight, class line 1
        var wizard = this._context.CreateNew<CharacterClass>(); // number 0: class line 0

        // The attributes of the buyer were created from the mocked class; only the qualification uses the class now.
        this._buyer.SelectedCharacter!.CharacterClass = wizard;

        var sword = this.CreateDefinition(0, 1, "Sword", [0, 1], wizard, this._knight);
        var shield = this.CreateDefinition(6, 1, "Shield", [1], this._knight);
        var helm = this.CreateDefinition(7, 1, "Helm", [2], wizard);
        var wings = this.CreateDefinition(12, 1, "Wings", [7], wizard);
        var ring = this.CreateDefinition(13, 8, "Ring", [10, 11], wizard);
        var pendant = this.CreateDefinition(13, 12, "Pendant", [9], wizard);
        var pet = this.CreateDefinition(13, 0, "Angel", [8], wizard);
        var heavyHelm = this.CreateDefinition(7, 2, "Heavy helm", [2], wizard);
        var levelRequirement = this._context.CreateNew<AttributeRequirement>();
        levelRequirement.Attribute = Stats.Level;
        levelRequirement.MinimumValue = 400;
        heavyHelm.Requirements.Add(levelRequirement);

        this._listings = [];
        await this.ListAsync("plain sword", sword, 10_000, 0).ConfigureAwait(false);
        await this.ListAsync("excellent sword", sword, 30_000, 9, item =>
        {
            this.AddExcellent(item, 1);
            this.AddExcellent(item, 4);
            item.HasSkill = true;
        }).ConfigureAwait(false);
        await this.ListAsync("ancient socket sword", sword, 20_000, 13, item =>
        {
            var set = this._context.CreateNew<ItemOfItemSet>();
            set.AncientSetDiscriminator = 2;
            item.ItemSetGroups.Add(set);
            item.SocketCount = 3;
            this.AddLuck(item);
        }).ConfigureAwait(false);
        await this.ListAsync("shield", shield, 5_000, 0).ConfigureAwait(false);
        await this.ListAsync("helm", helm, 6_000, 0).ConfigureAwait(false);
        await this.ListAsync("wings", wings, 7_000, 0, item => this.AddExcellent(item, 1)).ConfigureAwait(false);
        await this.ListAsync("ring", ring, 8_000, 0).ConfigureAwait(false);
        await this.ListAsync("pendant", pendant, 9_000, 0).ConfigureAwait(false);
        await this.ListAsync("pet", pet, 11_000, 0).ConfigureAwait(false);
        await this.ListAsync("heavy helm", heavyHelm, 12_000, 0).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the context.
    /// </summary>
    [TearDown]
    public void TearDown() => this._context.Dispose();

    /// <summary>
    /// Each slot kind maps to its listings; shields are group 6, pets, rings and pendants (all group 13) by their slot.
    /// </summary>
    /// <param name="slots">The slot kinds.</param>
    /// <param name="expected">The expected listings.</param>
    [TestCase(AuctionHouseEquipmentSlots.Weapon, "plain sword,excellent sword,ancient socket sword")]
    [TestCase(AuctionHouseEquipmentSlots.Shield, "shield")]
    [TestCase(AuctionHouseEquipmentSlots.Helm, "helm,heavy helm")]
    [TestCase(AuctionHouseEquipmentSlots.Wings, "wings")]
    [TestCase(AuctionHouseEquipmentSlots.Ring, "ring")]
    [TestCase(AuctionHouseEquipmentSlots.Pendant, "pendant")]
    [TestCase(AuctionHouseEquipmentSlots.Pet, "pet")]
    [TestCase(AuctionHouseEquipmentSlots.Ring | AuctionHouseEquipmentSlots.Pendant, "ring,pendant")]
    public async Task SearchBySlotAsync(AuctionHouseEquipmentSlots slots, string expected)
    {
        await this.AssertSearchAsync(AnyItem with { Slots = slots }, expected).ConfigureAwait(false);
    }

    /// <summary>
    /// The group mask has one bit per item group.
    /// </summary>
    [Test]
    public async Task SearchByGroupAsync()
    {
        await this.AssertSearchAsync(AnyItem with { GroupMask = (1 << 6) | (1 << 12) }, "shield,wings").ConfigureAwait(false);
    }

    /// <summary>
    /// The class line matches the qualified classes of every evolution of the base class.
    /// </summary>
    [Test]
    public async Task SearchByClassLineAsync()
    {
        await this.AssertSearchAsync(AnyItem with { ClassLine = AuctionHouseClassLine.DarkKnight }, "shield,plain sword,ancient socket sword,excellent sword").ConfigureAwait(false);
    }

    /// <summary>
    /// "Can equip now" uses the requirements and class qualification of the requester; the row flag says the same.
    /// </summary>
    [Test]
    public async Task SearchByCanEquipNowAsync()
    {
        var page = await this._market.SearchAsync(this._buyer, AnyItem with { Options = AuctionHouseSearchOptions.CanEquipNow }).ConfigureAwait(false);

        Assert.That(this.Names(page), Is.EquivalentTo(new[] { "plain sword", "excellent sword", "ancient socket sword", "helm", "wings", "ring", "pendant", "pet" }));
        var all = await this._market.SearchAsync(this._buyer, AnyItem).ConfigureAwait(false);
        Assert.That(all.Listings.Single(row => row.ListingId == this._listings["heavy helm"]).RequirementsMet, Is.False, "level requirement not met");
        Assert.That(all.Listings.Single(row => row.ListingId == this._listings["shield"]).RequirementsMet, Is.False, "class not qualified");
        Assert.That(all.Listings.Single(row => row.ListingId == this._listings["helm"]).RequirementsMet, Is.True);
    }

    /// <summary>
    /// Luck and skill are options of the item.
    /// </summary>
    [Test]
    public async Task SearchByLuckAndSkillAsync()
    {
        await this.AssertSearchAsync(AnyItem with { Options = AuctionHouseSearchOptions.Luck }, "ancient socket sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { Options = AuctionHouseSearchOptions.Skill }, "excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { Options = AuctionHouseSearchOptions.Luck | AuctionHouseSearchOptions.Skill }, string.Empty).ConfigureAwait(false);
    }

    /// <summary>
    /// The families test the item; one item matches several families.
    /// </summary>
    [Test]
    public async Task SearchByFamilyAsync()
    {
        await this.AssertSearchAsync(AnyItem with { Family = AuctionHouseItemFamily.Excellent }, "wings,excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { Family = AuctionHouseItemFamily.Ancient }, "ancient socket sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { Family = AuctionHouseItemFamily.Socket }, "ancient socket sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { Family = AuctionHouseItemFamily.Normal }, "shield,helm,ring,pendant,plain sword,pet,heavy helm").ConfigureAwait(false);
    }

    /// <summary>
    /// The upgrade range, the excellent bits, the excellent count, the sockets and the maximum price.
    /// </summary>
    [Test]
    public async Task SearchByLevelOptionsSocketsAndPriceAsync()
    {
        await this.AssertSearchAsync(AnyItem with { UpgradeMin = 9, UpgradeMax = 12 }, "excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { ExcellentMask = 0b1001 }, "excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { ExcellentMask = 0b0001 }, "wings,excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { MinExcellentCount = 2 }, "excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { MinSockets = 3 }, "ancient socket sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { MaxPrice = 6_000 }, "shield,helm").ConfigureAwait(false);
    }

    /// <summary>
    /// Name entries restrict the item types; ancient set entries require the discriminator; both kinds apply together.
    /// </summary>
    [Test]
    public async Task SearchByEntriesAsync()
    {
        var swordType = AuctionHouseItemTypes.GetItemType(0, 1);
        var helmType = AuctionHouseItemTypes.GetItemType(7, 1);
        await this.AssertSearchAsync(AnyItem with { NameMatchTypes = [swordType, helmType] }, "helm,plain sword,ancient socket sword,excellent sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { AncientSetMembers = [new AuctionAncientSetMember(swordType, 2)] }, "ancient socket sword").ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { AncientSetMembers = [new AuctionAncientSetMember(swordType, 1)] }, string.Empty).ConfigureAwait(false);
        await this.AssertSearchAsync(AnyItem with { NameMatchTypes = [helmType], AncientSetMembers = [new AuctionAncientSetMember(swordType, 2)] }, string.Empty).ConfigureAwait(false);
    }

    /// <summary>
    /// Results are sorted by price in both directions; pages have the page size of the rules, a page past the end is
    /// empty, and the total counts all matches. Rows carry the seller, the time left and the own-listing flag.
    /// </summary>
    [Test]
    public async Task SearchSortsAndPagesAsync()
    {
        this._helper.Rules.PageSize = 4;

        var first = await this._market.SearchAsync(this._buyer, AnyItem).ConfigureAwait(false);
        var third = await this._market.SearchAsync(this._buyer, AnyItem with { Page = 2 }).ConfigureAwait(false);
        var past = await this._market.SearchAsync(this._buyer, AnyItem with { Page = 3 }).ConfigureAwait(false);
        var descending = await this._market.SearchAsync(this._buyer, AnyItem with { Sort = AuctionHouseSortOrder.PriceDescending }).ConfigureAwait(false);
        var own = await this._market.SearchAsync(this._seller, AnyItem).ConfigureAwait(false);

        Assert.That(first.TotalCount, Is.EqualTo(10));
        Assert.That(first.Listings.Select(row => row.Price), Is.EqualTo(new uint[] { 5_000, 6_000, 7_000, 8_000 }));
        Assert.That(third.Listings.Select(row => row.Price), Is.EqualTo(new uint[] { 20_000, 30_000 }));
        Assert.That(past.Listings, Is.Empty);
        Assert.That(past.TotalCount, Is.EqualTo(10));
        Assert.That(descending.Listings.First().Price, Is.EqualTo(30_000));
        Assert.That(first.Listings.First().SellerName, Is.EqualTo("seller"));
        Assert.That(first.Listings.First().SecondsLeft, Is.InRange(48 * 3600 - 60, 48 * 3600));
        Assert.That(first.Listings.All(row => !row.IsOwnListing), Is.True);
        Assert.That(own.Listings.All(row => row.IsOwnListing), Is.True);
    }

    /// <summary>
    /// The goods list aggregates the goods of other sellers per type and level, leaves the requester's own listings
    /// out, filters by type, orders by type and level, and pages.
    /// </summary>
    [Test]
    public async Task GoodsListAggregatesOtherSellersAsync()
    {
        var other = await this._helper.CreatePlayerAsync(this._gameContext, "other", 100_000_000).ConfigureAwait(false);
        await this.ListGoodsAsync(this._seller, this._helper.JewelOfBless, 5, 1_000_000).ConfigureAwait(false);
        await this.ListGoodsAsync(other, this._helper.JewelOfBless, 3, 900_000).ConfigureAwait(false);
        await this.ListGoodsAsync(other, this._helper.JewelOfSoul, 2, 2_000_000).ConfigureAwait(false);
        await this.ListGoodsAsync(this._buyer, this._helper.JewelOfSoul, 4, 1_000_000).ConfigureAwait(false);
        await this.ListGoodsAsync(other, this._helper.PackedJewelOfBless, 1, 9_000_000, 1).ConfigureAwait(false);

        var all = await this._market.GetGoodsListAsync(this._buyer, new AuctionGoodsListQuery(0, [])).ConfigureAwait(false);
        var filtered = await this._market.GetGoodsListAsync(this._buyer, new AuctionGoodsListQuery(0, [this._helper.JewelOfBlessType])).ConfigureAwait(false);
        this._helper.Rules.PageSize = 2;
        var secondPage = await this._market.GetGoodsListAsync(this._buyer, new AuctionGoodsListQuery(1, [])).ConfigureAwait(false);

        var packedType = AuctionHouseItemTypes.GetItemType(this._helper.PackedJewelOfBless);
        var soulType = AuctionHouseItemTypes.GetItemType(this._helper.JewelOfSoul);
        Assert.That(all.TotalCount, Is.EqualTo(3));
        Assert.That(all.Goods, Is.EqualTo(new[]
        {
            new AuctionGoodsRow(packedType, 1, 9_000_000, 1, 1),
            new AuctionGoodsRow(this._helper.JewelOfBlessType, 0, 900_000, 8, 2),
            new AuctionGoodsRow(soulType, 0, 2_000_000, 2, 1),
        }));
        Assert.That(filtered.Goods.Select(row => row.ItemType), Is.EqualTo(new[] { this._helper.JewelOfBlessType }));
        Assert.That(secondPage.Goods.Select(row => row.ItemType), Is.EqualTo(new[] { soulType }));
    }

    /// <summary>
    /// The quote has at most 8 tiers, cheapest first, and refuses totals above the maximum price.
    /// </summary>
    [Test]
    public async Task QuoteHasAtMostEightTiersAndRespectsMaximumPriceAsync()
    {
        for (var i = 0; i < 9; i++)
        {
            var other = await this._helper.CreatePlayerAsync(this._gameContext, "goods" + i, 100_000_000).ConfigureAwait(false);
            await this.ListGoodsAsync(other, this._helper.JewelOfBless, 2, (uint)(1_000_000 + (i * 100_000))).ConfigureAwait(false);
        }

        var quote = await this._market.QuoteGoodsAsync(this._buyer, new AuctionGoodsQuoteQuery(this._helper.JewelOfBlessType, 0, 3)).ConfigureAwait(false);
        this._helper.Rules.MaximumPrice = 2_000_000;
        var tooExpensive = await this._market.QuoteGoodsAsync(this._buyer, new AuctionGoodsQuoteQuery(this._helper.JewelOfBlessType, 0, 3)).ConfigureAwait(false);

        Assert.That(quote.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(quote.Total, Is.EqualTo(3_100_000));
        Assert.That(quote.Tiers, Has.Count.EqualTo(8));
        Assert.That(quote.Tiers[0], Is.EqualTo(new AuctionPriceTier(1_000_000, 2)));
        Assert.That(tooExpensive.Result, Is.EqualTo(AuctionHouseResult.PriceOutOfRange));
        Assert.That(tooExpensive.Total, Is.Zero);
    }

    /// <summary>
    /// Own listings are the active listings of the requester, with item data for item listings only.
    /// </summary>
    [Test]
    public async Task OwnListingsAreTheActiveListingsOfTheRequesterAsync()
    {
        var goods = await this.ListGoodsAsync(this._seller, this._helper.JewelOfBless, 5, 1_000_000).ConfigureAwait(false);
        await this._market.CancelListingAsync(this._seller, new AuctionCancelListingOrder(this._listings["helm"])).ConfigureAwait(false);

        var own = await this._market.GetOwnListingsAsync(this._seller).ConfigureAwait(false);
        var none = await this._market.GetOwnListingsAsync(this._buyer).ConfigureAwait(false);

        Assert.That(own.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(own.Listings, Has.Count.EqualTo(10));
        Assert.That(own.Listings.Any(row => row.ListingId == this._listings["helm"]), Is.False);
        var goodsRow = own.Listings.Single(row => row.ListingId == goods);
        Assert.That(goodsRow.Kind, Is.EqualTo(AuctionHouseListingKind.Goods));
        Assert.That(goodsRow.Item, Is.Null);
        Assert.That(goodsRow.Listed, Is.EqualTo(5));
        Assert.That(own.Listings.Where(row => row.Kind == AuctionHouseListingKind.Item).All(row => row.Item is not null), Is.True);
        Assert.That(none.Listings, Is.Empty);
    }

    /// <summary>
    /// The collection pages the entries of the requester from the database, oldest first, with item data for single
    /// items and the display copies of sold items; the seller summary counts active listings and waiting entries.
    /// </summary>
    [Test]
    public async Task CollectionAndSummaryAsync()
    {
        await this._helper.FillInventoryAsync(this._buyer).ConfigureAwait(false);
        await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(this._listings["helm"], 6_000)).ConfigureAwait(false);
        await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(this._listings["ring"], 8_000)).ConfigureAwait(false);
        this._helper.Rules.PageSize = 1;

        var buyerFirst = await this._market.GetCollectionAsync(this._buyer, new AuctionCollectionQuery(0)).ConfigureAwait(false);
        var buyerPast = await this._market.GetCollectionAsync(this._buyer, new AuctionCollectionQuery(2)).ConfigureAwait(false);
        this._helper.Rules.PageSize = 20;
        var seller = await this._market.GetCollectionAsync(this._seller, new AuctionCollectionQuery(0)).ConfigureAwait(false);
        var summary = await this._market.GetSellerSummaryAsync(this._seller).ConfigureAwait(false);

        Assert.That(buyerFirst.TotalCount, Is.EqualTo(2));
        var purchase = buyerFirst.Entries.Single();
        Assert.That(purchase.Kind, Is.EqualTo(AuctionHouseCollectionEntryKind.Purchase));
        Assert.That(purchase.Quantity, Is.EqualTo(1));
        Assert.That(purchase.Item?.Definition?.Name.ToString(), Is.EqualTo("Helm"));
        Assert.That(buyerPast.Entries, Is.Empty);
        Assert.That(seller.Entries.Select(entry => entry.Kind), Is.All.EqualTo(AuctionHouseCollectionEntryKind.Proceeds));
        Assert.That(seller.Entries.Select(entry => entry.Net), Is.EquivalentTo(new uint[] { 5_400, 7_200 }));
        Assert.That(seller.Entries.Select(entry => entry.Item?.Definition?.Name.ToString()), Is.EquivalentTo(new[] { "Helm", "Ring" }), "the proceeds show the display copies of the sold items");
        Assert.That(summary, Is.EqualTo(new AuctionSellerSummary(8, 2)));
    }

    private async Task AssertSearchAsync(AuctionSearchQuery query, string expected)
    {
        var page = await this._market.SearchAsync(this._buyer, query).ConfigureAwait(false);
        Assert.That(page.Result, Is.EqualTo(AuctionHouseResult.Success));
        var expectedNames = expected.Length == 0 ? [] : expected.Split(',');
        Assert.That(this.Names(page), Is.EquivalentTo(expectedNames), query.ToString());
        Assert.That(page.TotalCount, Is.EqualTo(expectedNames.Length));
    }

    private IEnumerable<string> Names(AuctionSearchPage page)
        => page.Listings.Select(row => this._listings.Single(pair => pair.Value == row.ListingId).Key);

    private ItemDefinition CreateDefinition(byte group, short number, string name, int[] slots, params CharacterClass[] classes)
    {
        var definition = this._context.CreateNew<ItemDefinition>();
        definition.Group = group;
        definition.Number = number;
        definition.Name = name;
        definition.Width = 1;
        definition.Height = 1;
        definition.Durability = 20;
        definition.ItemSlot = this._context.CreateNew<ItemSlotType>();
        foreach (var slot in slots)
        {
            definition.ItemSlot.ItemSlots.Add(slot);
        }

        foreach (var characterClass in classes)
        {
            definition.QualifiedCharacters.Add(characterClass);
        }

        definition.PossibleItemOptions.Add(this._excellentOptions);
        this._helper.Configuration.Items.Add(definition);
        return definition;
    }

    private void AddExcellent(Item item, int number)
    {
        var link = this._context.CreateNew<ItemOptionLink>();
        link.ItemOption = this._excellentOptions.PossibleOptions.Single(option => option.Number == number);
        item.ItemOptions.Add(link);
    }

    private void AddLuck(Item item)
    {
        var link = this._context.CreateNew<ItemOptionLink>();
        link.ItemOption = this._luck;
        item.ItemOptions.Add(link);
    }

    private async Task ListAsync(string name, ItemDefinition definition, uint price, byte level, Action<Item>? configure = null)
    {
        var item = await this._helper.AddItemAsync(this._seller, definition, Slot, level).ConfigureAwait(false);
        configure?.Invoke(item);
        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, price, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success), name);
        this._listings[name] = outcome.ListingId;
    }

    private async Task<Guid> ListGoodsAsync(Player seller, ItemDefinition definition, int quantity, uint unitPrice, byte level = 0)
    {
        for (var i = 0; i < quantity; i++)
        {
            await this._helper.AddItemAsync(seller, definition, (byte)(Slot + 20 + i), level).ConfigureAwait(false);
        }

        var outcome = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot + 20, (uint)quantity, unitPrice, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        return outcome.ListingId;
    }
}
