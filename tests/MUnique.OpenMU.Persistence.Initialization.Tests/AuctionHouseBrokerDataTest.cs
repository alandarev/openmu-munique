// <copyright file="AuctionHouseBrokerDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that Wandering Merchant Zyro is the auction house broker and the auction house is active, in new databases
/// and, by the <see cref="AddAuctionHouseBrokerUpdatePlugIn"/>, in existing ones.
/// </summary>
[TestFixture]
internal class AuctionHouseBrokerDataTest
{
    private const short ZyroNumber = 568;

    private static readonly Guid[] AuctionHousePlugInIds =
    [
        typeof(AuctionHouseFeaturePlugIn).GUID,
        typeof(AuctionHouseExpiryPlugIn).GUID,
        typeof(AuctionHouseLoginNoticePlugIn).GUID,
    ];

    /// <summary>
    /// A new database has Zyro as broker and the active auction house with the default rules.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasActiveAuctionHouseAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);

        await AssertBrokerAndActivePlugInsAsync(contextProvider, gameConfiguration).ConfigureAwait(false);
        var zyro = gameConfiguration.Monsters.Single(monster => monster.Number == ZyroNumber);
        Assert.That(zyro.MerchantStore?.Items.Count ?? 0, Is.Zero, "a merchant store would win over the broker window");
    }

    /// <summary>
    /// The update turns Zyro into the broker and activates the auction house with the default rules in a database
    /// which had the feature disabled with other rules; applying it twice gives the same result.
    /// </summary>
    [Test]
    public async Task UpdateActivatesAuctionHouseIdempotentlyAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        gameConfiguration.Monsters.Single(monster => monster.Number == ZyroNumber).NpcWindow = NpcWindow.NpcDialog;
        var feature = gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(AuctionHouseFeaturePlugIn).GUID);
        feature.IsActive = false;
        feature.SetConfiguration(new AuctionHouseRules { SaleFeeBasisPoints = 1 }, null);
        var expiry = gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(AuctionHouseExpiryPlugIn).GUID);
        gameConfiguration.PlugInConfigurations.Remove(expiry);

        var update = new AddAuctionHouseBrokerUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
            await AssertBrokerAndActivePlugInsAsync(contextProvider, gameConfiguration).ConfigureAwait(false);
        }

        Assert.That(update.Version, Is.EqualTo(UpdateVersion.AddAuctionHouseBroker));
        Assert.That((int)UpdateVersion.AddAuctionHouseBroker, Is.EqualTo(10011));
    }

    /// <summary>
    /// The update keeps the rules of an auction house which its operator activated already.
    /// </summary>
    [Test]
    public async Task UpdateKeepsRulesOfActiveFeatureAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        var feature = gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(AuctionHouseFeaturePlugIn).GUID);
        feature.SetConfiguration(new AuctionHouseRules { SaleFeeBasisPoints = 1 }, null);

        await new AddAuctionHouseBrokerUpdatePlugIn().ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        Assert.That(feature.IsActive, Is.True);
        Assert.That(feature.GetConfiguration<AuctionHouseRules>(null)!.SaleFeeBasisPoints, Is.EqualTo(1));
    }

    /// <summary>
    /// A new database has Zyro standing in Lorencia, Devias and Elbeland, not wandering between them.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasStationaryBrokerInThreeTownsAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        AssertStationaryBroker(gameConfiguration);
    }

    /// <summary>
    /// The update turns Zyro's wandering spawns of an existing database into automatic ones, leaves the other wandering
    /// merchants alone, and gives the same result when applied twice.
    /// </summary>
    [Test]
    public async Task UpdateMakesBrokerStationaryIdempotentlyAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        foreach (var spawn in GetZyroSpawns(gameConfiguration))
        {
            spawn.SpawnTrigger = SpawnTrigger.Wandering;
        }

        var otherWandering = gameConfiguration.Maps.SelectMany(map => map.MonsterSpawns)
            .Where(spawn => spawn.SpawnTrigger == SpawnTrigger.Wandering && spawn.MonsterDefinition?.Number != ZyroNumber)
            .ToList();
        Assert.That(otherWandering, Is.Not.Empty, "the other wandering merchants");

        var update = new MakeAuctionBrokerStationaryUpdatePlugIn();
        for (var i = 0; i < 2; i++)
        {
            await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
            AssertStationaryBroker(gameConfiguration);
        }

        Assert.That(otherWandering.Select(spawn => spawn.SpawnTrigger), Is.All.EqualTo(SpawnTrigger.Wandering));
        Assert.That(update.IsMandatory, Is.True);
        Assert.That((int)UpdateVersion.MakeAuctionBrokerStationary, Is.EqualTo(10012));
    }

    private static List<MonsterSpawnArea> GetZyroSpawns(GameConfiguration gameConfiguration)
        => gameConfiguration.Maps.SelectMany(map => map.MonsterSpawns).Where(spawn => spawn.MonsterDefinition?.Number == ZyroNumber).ToList();

    private static void AssertStationaryBroker(GameConfiguration gameConfiguration)
    {
        var spawns = GetZyroSpawns(gameConfiguration);
        Assert.That(spawns.Select(spawn => (spawn.GameMap!.Number, spawn.X1, spawn.Y1)), Is.EquivalentTo(new (short, byte, byte)[] { (0, 131, 139), (2, 225, 52), (51, 55, 199) }));
        Assert.That(spawns.Select(spawn => spawn.SpawnTrigger), Is.All.EqualTo(SpawnTrigger.Automatic));
    }

    private static async Task AssertBrokerAndActivePlugInsAsync(InMemoryPersistenceContextProvider contextProvider, GameConfiguration gameConfiguration)
    {
        var dataSource = new GameConfigurationDataSource(new NullLogger<GameConfigurationDataSource>(), contextProvider);
        await dataSource.GetOwnerAsync(gameConfiguration.GetId()).ConfigureAwait(false);
        var referenceHandler = new ByDataSourceReferenceHandler(dataSource);
        Assert.That(gameConfiguration.Monsters.Single(monster => monster.Number == ZyroNumber).NpcWindow, Is.EqualTo(NpcWindow.AuctionHouse));
        foreach (var id in AuctionHousePlugInIds)
        {
            var configurations = gameConfiguration.PlugInConfigurations.Where(c => c.TypeId == id).ToList();
            Assert.That(configurations, Has.Count.EqualTo(1), id.ToString());
            Assert.That(configurations[0].IsActive, Is.True, id.ToString());
        }

        // The initialization writes the configuration with reference metadata, like the server reads it.
        var rules = gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeof(AuctionHouseFeaturePlugIn).GUID).GetConfiguration<AuctionHouseRules>(referenceHandler);
        Assert.That(rules, Is.Not.Null);
        Assert.That(rules!.SaleFeeBasisPoints, Is.EqualTo(AuctionHouseRules.Default.SaleFeeBasisPoints));
        Assert.That(rules.PostingFeeBasisPoints, Is.EqualTo(500));
        Assert.That(rules.DurationHours, Is.EqualTo(new byte[] { 48 }));
        Assert.That(rules.Goods, Has.Count.EqualTo(20));
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
