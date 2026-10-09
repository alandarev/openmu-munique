// <copyright file="AuctionHouseMarketTestHelper.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Creates game contexts, players and items for tests of the <see cref="AuctionHouseMarket"/> with the in-memory
/// persistence.
/// </summary>
internal sealed class AuctionHouseMarketTestHelper
{
    /// <summary>
    /// The first inventory slot behind the equipped slots.
    /// </summary>
    public const byte FirstInventorySlot = 12;

    /// <summary>
    /// Creates the rules of the market tests: the operator defaults, but with a posting fee of 5 %, which the expected
    /// amounts of the tests are based on. The defaults themselves are covered by
    /// <see cref="AuctionHouseFeesTest.DefaultRules"/> and <see cref="AuctionHouseMarketBuyingTest.DefaultFeesAsync"/>.
    /// </summary>
    /// <returns>The rules of the market tests.</returns>
    public static AuctionHouseRules CreateRules() => new() { PostingFeeBasisPoints = 500 };

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseMarketTestHelper"/> class with one database and game
    /// configuration.
    /// </summary>
    /// <param name="rules">The rules; <see cref="CreateRules"/> if <see langword="null"/>.</param>
    public AuctionHouseMarketTestHelper(AuctionHouseRules? rules = null)
    {
        this.Rules = rules ?? CreateRules();
        this.ContextProvider = new InMemoryPersistenceContextProvider();
        var context = this.ContextProvider.CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        var map = context.CreateNew<GameMapDefinition>();
        map.Number = 0;
        map.TerrainData = new byte[ushort.MaxValue + 3];
        configuration.Maps.Add(map);
        configuration.MaximumInventoryMoney = int.MaxValue;
        configuration.RecoveryInterval = int.MaxValue;
        configuration.MaximumPartySize = 5;
        configuration.ItemDropDuration = TimeSpan.FromMinutes(1);
        this.Configuration = configuration;

        this.JewelOfBless = this.CreateDefinition(context, 14, 13, "Jewel of Bless", 1, 1, 1);
        this.JewelOfSoul = this.CreateDefinition(context, 14, 14, "Jewel of Soul", 1, 1, 1);
        this.PackedJewelOfBless = this.CreateDefinition(context, 12, 30, "Packed Jewel of Bless", 1, 1, 1);
        this.Kris = this.CreateDefinition(context, 0, 0, "Kris", 1, 2, 20);
        this.Kris.ItemSlot = context.CreateNew<ItemSlotType>();
        this.SmallHealingPotion = this.CreateDefinition(context, 14, 0, "Small Healing Potion", 1, 1, 3);
        this.BoundItem = this.CreateDefinition(context, 13, 20, "Bound ring", 1, 1, 1);
        this.BoundItem.IsBoundToCharacter = true;
        this.UntradableItem = this.CreateDefinition(context, 14, 21, "Untradable", 1, 1, 1);
        this.UntradableItem.IsTradable = false;
    }

    /// <summary>
    /// Gets the persistence context provider: the database.
    /// </summary>
    public InMemoryPersistenceContextProvider ContextProvider { get; }

    /// <summary>
    /// Gets the game configuration.
    /// </summary>
    public GameConfiguration Configuration { get; }

    /// <summary>
    /// Gets the rules of the auction house feature of the created game contexts.
    /// </summary>
    public AuctionHouseRules Rules { get; }

    /// <summary>
    /// Gets the definition of the Jewel of Bless, goods.
    /// </summary>
    public ItemDefinition JewelOfBless { get; }

    /// <summary>
    /// Gets the definition of the Jewel of Soul, goods.
    /// </summary>
    public ItemDefinition JewelOfSoul { get; }

    /// <summary>
    /// Gets the definition of the packed Jewel of Bless, goods.
    /// </summary>
    public ItemDefinition PackedJewelOfBless { get; }

    /// <summary>
    /// Gets the definition of the Kris, a wearable weapon of 1x2 slots.
    /// </summary>
    public ItemDefinition Kris { get; }

    /// <summary>
    /// Gets the definition of a stackable potion.
    /// </summary>
    public ItemDefinition SmallHealingPotion { get; }

    /// <summary>
    /// Gets the definition of an item which is bound to its character.
    /// </summary>
    public ItemDefinition BoundItem { get; }

    /// <summary>
    /// Gets the definition of an item which is not tradable.
    /// </summary>
    public ItemDefinition UntradableItem { get; }

    /// <summary>
    /// Gets the item type of the Jewel of Bless.
    /// </summary>
    public ushort JewelOfBlessType => AuctionHouseItemTypes.GetItemType(this.JewelOfBless);

    /// <summary>
    /// Creates a game context with the active auction house feature, without an explicit market.
    /// </summary>
    /// <returns>The game context.</returns>
    public IGameContext CreateGameContext()
    {
        var mapInitializer = new MapInitializer(this.Configuration, new Microsoft.Extensions.Logging.Abstractions.NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(this.Configuration, this.ContextProvider, mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        gameContext.FeaturePlugIns.AddPlugIn(new AuctionHouseFeaturePlugIn { Configuration = this.Rules }, true);
        return gameContext;
    }

    /// <summary>
    /// Creates a player with its own character.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="name">The character name.</param>
    /// <param name="money">The inventory Zen.</param>
    /// <returns>The player.</returns>
    public async ValueTask<Player> CreatePlayerAsync(IGameContext gameContext, string name, int money = 0)
    {
        var character = new AuctionTestCharacter { Name = name };
        character.Inventory = this.ContextProvider.CreateNewContext().CreateNew<ItemStorage>();
        character.Inventory.Money = money;
        return await PlayerTestHelper.CreatePlayerAsync(gameContext, character).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a new item to the inventory of a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="definition">The item definition.</param>
    /// <param name="slot">The inventory slot.</param>
    /// <param name="level">The item level.</param>
    /// <returns>The item.</returns>
    public async ValueTask<Item> AddItemAsync(Player player, ItemDefinition definition, byte slot, byte level = 0)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = level;
        item.Durability = definition.Durability;
        Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True, $"slot {slot} is not free");
        return item;
    }

    /// <summary>
    /// Adds new items of a definition to consecutive inventory slots.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="definition">The item definition.</param>
    /// <param name="count">The count.</param>
    /// <param name="firstSlot">The first slot.</param>
    /// <returns>The items.</returns>
    public async ValueTask<List<Item>> AddItemsAsync(Player player, ItemDefinition definition, int count, byte firstSlot = FirstInventorySlot)
    {
        var items = new List<Item>();
        for (var i = 0; i < count; i++)
        {
            items.Add(await this.AddItemAsync(player, definition, (byte)(firstSlot + i)).ConfigureAwait(false));
        }

        return items;
    }

    /// <summary>
    /// Fills every free inventory slot with Jewels of Soul.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask FillInventoryAsync(Player player)
    {
        while (player.Inventory!.CheckInvSpace(this.CreateTemporaryItem(this.JewelOfSoul)) is { } slot)
        {
            await this.AddItemAsync(player, this.JewelOfSoul, slot).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the collection entries of a character.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The entries.</returns>
    public async ValueTask<List<AuctionCollectionEntry>> GetEntriesAsync(Character character)
    {
        using var context = this.ContextProvider.CreateNewContext();
        return (await context.GetAsync<AuctionCollectionEntry>().ConfigureAwait(false)).Where(entry => entry.OwnerCharacterId == character.GetId()).ToList();
    }

    /// <summary>
    /// Gets all sale records.
    /// </summary>
    /// <returns>The sale records.</returns>
    public async ValueTask<List<AuctionSale>> GetSalesAsync()
    {
        using var context = this.ContextProvider.CreateNewContext();
        return (await context.GetAsync<AuctionSale>().ConfigureAwait(false)).ToList();
    }

    /// <summary>
    /// Gets a listing.
    /// </summary>
    /// <param name="id">The listing id.</param>
    /// <returns>The listing.</returns>
    public async ValueTask<AuctionListing?> GetListingAsync(Guid id)
    {
        using var context = this.ContextProvider.CreateNewContext();
        return await context.GetByIdAsync<AuctionListing>(id).ConfigureAwait(false);
    }

    private Item CreateTemporaryItem(ItemDefinition definition)
    {
        return new TemporaryItem { Definition = definition };
    }

    private ItemDefinition CreateDefinition(IContext context, byte group, short number, string name, byte width, byte height, byte durability)
    {
        var definition = context.CreateNew<ItemDefinition>();
        definition.Group = group;
        definition.Number = number;
        definition.Name = name;
        definition.Width = width;
        definition.Height = height;
        definition.Durability = durability;
        this.Configuration.Items.Add(definition);
        return definition;
    }
}
