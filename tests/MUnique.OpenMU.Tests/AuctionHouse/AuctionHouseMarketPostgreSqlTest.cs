// <copyright file="AuctionHouseMarketPostgreSqlTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
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

        var outcomes = await Task.WhenAll(
            Task.Run(async () => await this._market.BuyListingAsync(first, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false)),
            Task.Run(async () => await this._market.BuyListingAsync(second, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false))).ConfigureAwait(false);

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
        await this._market.CreateListingAsync(cheapSeller, new AuctionCreateListingOrder(Slot, 10, 1_000_000, 48, 0)).ConfigureAwait(false);
        await this._market.CreateListingAsync(dearSeller, new AuctionCreateListingOrder(Slot, 10, 1_100_000, 48, 0)).ConfigureAwait(false);

        var outcomes = await Task.WhenAll(
            Task.Run(async () => await this._market.BuyGoodsAsync(first, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 8, 20_000_000)).ConfigureAwait(false)),
            Task.Run(async () => await this._market.BuyGoodsAsync(second, new AuctionBuyGoodsOrder(this.JewelOfBlessType, 0, 8, 20_000_000)).ConfigureAwait(false))).ConfigureAwait(false);

        Assert.That(outcomes.Select(outcome => outcome.Result), Has.Some.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcomes.Select(outcome => outcome.Result), Is.All.AnyOf(AuctionHouseResult.Success, AuctionHouseResult.QuoteChanged));
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
    /// A failure of the commit after all in-memory changes (the database refuses the sale record) rolls everything
    /// back: the buyer's Zen and inventory are restored, the listing is unchanged, and the next purchase succeeds.
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
    /// A purchase racing the expiry of the same listing: exactly one of them takes it, and the database is consistent.
    /// </summary>
    [Test]
    public async Task PurchaseRacingExpiryHasOneOutcomeAsync()
    {
        var seller = await this.CreatePlayerAsync("seller", 100_000_000, (context, inventory) => AddItemsAsync(context, inventory, this._kris, 1)).ConfigureAwait(false);
        var buyer = await this.CreatePlayerAsync("buyer", 50_000_000).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        var expiryMarket = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), new ShiftedTimeProvider(TimeSpan.FromHours(49)));
        await expiryMarket.EnsureIndexLoadedAsync().ConfigureAwait(false);

        var buying = Task.Run(async () => await this._market.BuyListingAsync(buyer, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false));
        var expiring = Task.Run(async () => await expiryMarket.ExpireDueListingsAsync(true).ConfigureAwait(false));
        var bought = await buying.ConfigureAwait(false);
        var expired = await expiring.ConfigureAwait(false);

        Assert.That(bought.Result == AuctionHouseResult.Success ^ expired == 1, Is.True, $"bought {bought.Result}, expired {expired}");
        Assert.That(bought.Result, Is.AnyOf(AuctionHouseResult.Success, AuctionHouseResult.NotFound));
        await using var data = this._database.CreateDataContext();
        var listing = await data.Set<EfModel.AuctionListing>().SingleAsync().ConfigureAwait(false);
        var entries = await data.Set<EfModel.AuctionCollectionEntry>().ToListAsync().ConfigureAwait(false);
        if (bought.Result == AuctionHouseResult.Success)
        {
            Assert.That(listing.State, Is.EqualTo(AuctionListingState.SoldOut));
            Assert.That(await this.GetDatabaseMoneyAsync(buyer).ConfigureAwait(false), Is.EqualTo(40_000_000));
            Assert.That(entries.Select(entry => entry.Kind), Is.EqualTo(new[] { AuctionHouseCollectionEntryKind.Proceeds }));
        }
        else
        {
            Assert.That(listing.State, Is.EqualTo(AuctionListingState.Expired));
            Assert.That(await this.GetDatabaseMoneyAsync(buyer).ConfigureAwait(false), Is.EqualTo(50_000_000));
            Assert.That(entries.Select(entry => entry.Kind), Is.EqualTo(new[] { AuctionHouseCollectionEntryKind.Expired }));
            Assert.That(await data.Set<EfModel.Item>().CountAsync(item => item.ItemStorageId == entries[0].EscrowId).ConfigureAwait(false), Is.EqualTo(1));
        }

        Assert.That(await data.Set<EfModel.Item>().CountAsync().ConfigureAwait(false), Is.EqualTo(1), "the item exists exactly once");
    }

    /// <summary>
    /// Two expiry runs of different markets (processes) at the same time expire every due listing exactly once.
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

        var first = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), later);
        var second = new AuctionHouseMarket(this._database.ContextProvider, this._configuration, new NullLogger<AuctionHouseMarket>(), later);

        var counts = await Task.WhenAll(
            Task.Run(async () => await first.ExpireDueListingsAsync(true).ConfigureAwait(false)),
            Task.Run(async () => await second.ExpireDueListingsAsync(true).ConfigureAwait(false))).ConfigureAwait(false);
        var rest = await first.ExpireDueListingsAsync(true).ConfigureAwait(false) + await second.ExpireDueListingsAsync(true).ConfigureAwait(false);

        Assert.That(counts.Sum() + rest, Is.EqualTo(3));
        await using var data = this._database.CreateDataContext();
        Assert.That(await data.Set<EfModel.AuctionListing>().CountAsync(listing => listing.State == AuctionListingState.Expired).ConfigureAwait(false), Is.EqualTo(3));
        var entries = await data.Set<EfModel.AuctionCollectionEntry>().ToListAsync().ConfigureAwait(false);
        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries.Sum(entry => entry.Quantity), Is.EqualTo(6));
        Assert.That(entries.Select(entry => entry.OwnerCharacterId).Distinct().Count(), Is.EqualTo(3));
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

    private async ValueTask<long> GetDatabaseMoneyAsync(Player player)
    {
        var inventoryId = player.SelectedCharacter!.Inventory!.GetId();
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
}
