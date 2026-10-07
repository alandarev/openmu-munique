// <copyright file="AuctionHouseRulesTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests for the goods list of the <see cref="AuctionHouseRules"/> and the round-trip of the rules through the
/// plug-in configuration.
/// </summary>
[TestFixture]
public class AuctionHouseRulesTest
{
    private GameConfiguration _gameConfiguration = null!;

    /// <summary>
    /// Creates the Season 6 configuration.
    /// </summary>
    [OneTimeSetUp]
    public async Task SetupAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        this._gameConfiguration = (await contextProvider.CreateNewConfigurationContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>
    /// Each default goods entry names an item of the Season 6 configuration: the ten jewels and their packed forms.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="name">The name of the item definition.</param>
    [TestCase(14, 13, "Jewel of Bless")]
    [TestCase(14, 14, "Jewel of Soul")]
    [TestCase(14, 16, "Jewel of Life")]
    [TestCase(14, 22, "Jewel of Creation")]
    [TestCase(14, 31, "Jewel of Guardian")]
    [TestCase(12, 15, "Jewel of Chaos")]
    [TestCase(14, 42, "Jewel of Harmony")]
    [TestCase(14, 41, "Gemstone")]
    [TestCase(14, 43, "Lower refine stone")]
    [TestCase(14, 44, "Higher refine stone")]
    [TestCase(12, 30, "Packed Jewel of Bless")]
    [TestCase(12, 31, "Packed Jewel of Soul")]
    [TestCase(12, 136, "Packed Jewel of Life")]
    [TestCase(12, 137, "Packed Jewel of Creation")]
    [TestCase(12, 138, "Packed Jewel of Guardian")]
    [TestCase(12, 139, "Packed Gemstone")]
    [TestCase(12, 140, "Packed Jewel of Harmony")]
    [TestCase(12, 141, "Packed Jewel of Chaos")]
    [TestCase(12, 142, "Packed Lower refine stone")]
    [TestCase(12, 143, "Packed Higher refine stone")]
    public void DefaultGoodsAreTheJewelsAndTheirPackedForms(int group, int number, string name)
    {
        var definition = this._gameConfiguration.Items.Single(item => item.Group == group && item.Number == number);

        Assert.That(definition.Name.ToString(), Is.EqualTo(name));
        Assert.That(AuctionHouseRules.Default.Goods.Where(goods => goods.Group == group && goods.Number == number), Has.Exactly(1).Items);
        Assert.That(AuctionHouseRules.Default.IsGoods(definition), Is.True);
    }

    /// <summary>
    /// The default goods list has exactly the twenty jewel types; other items aren't goods.
    /// </summary>
    [Test]
    public void OtherItemsAreNotGoods()
    {
        var rules = AuctionHouseRules.Default;

        Assert.That(rules.Goods, Has.Count.EqualTo(20));
        Assert.That(this._gameConfiguration.Items.Count(rules.IsGoods), Is.EqualTo(20));
        Assert.That(rules.IsGoods(this._gameConfiguration.Items.Single(item => item.Group == 14 && item.Number == 1)), Is.False, "Small healing potion");
        Assert.That(rules.IsGoods(this._gameConfiguration.Items.Single(item => item.Group == 0 && item.Number == 0)), Is.False, "Kris");
    }

    /// <summary>
    /// Changed rules survive the round-trip through the plug-in configuration's JSON.
    /// </summary>
    [Test]
    public void RulesRoundTripThroughPlugInConfiguration()
    {
        var rules = new AuctionHouseRules
        {
            PostingFeeFlat = 1_000,
            PostingFeeBasisPoints = 250,
            SaleFeeBasisPoints = 700,
            DurationHours = [12, 48],
            MaximumActiveListings = 10,
            GoodsPriceSignificantDigits = 2,
            Goods = [new() { Group = 14, Number = 13, Name = "Jewel of Bless" }, new() { Group = 13, Number = 0, Name = "Guardian Angel" }],
        };
        var configuration = new Persistence.BasicModel.PlugInConfiguration();

        configuration.SetConfiguration(rules, null);
        var roundTripped = configuration.GetConfiguration<AuctionHouseRules>(null);

        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.PostingFeeFlat, Is.EqualTo(1_000));
        Assert.That(roundTripped.PostingFeeBasisPoints, Is.EqualTo(250));
        Assert.That(roundTripped.SaleFeeBasisPoints, Is.EqualTo(700));
        Assert.That(roundTripped.DurationHours, Is.EqualTo(new byte[] { 12, 48 }));
        Assert.That(roundTripped.MaximumActiveListings, Is.EqualTo(10));
        Assert.That(roundTripped.GoodsPriceSignificantDigits, Is.EqualTo(2));
        Assert.That(roundTripped.Goods.Select(goods => (goods.Group, goods.Number, goods.Name)), Is.EqualTo(new[] { ((byte)14, (short)13, "Jewel of Bless"), ((byte)13, (short)0, "Guardian Angel") }));
    }

    /// <summary>
    /// The default rules survive the round-trip through the plug-in configuration's JSON, including the goods list;
    /// a configuration without a goods list deserializes to the default list.
    /// </summary>
    [Test]
    public void DefaultRulesRoundTripThroughPlugInConfiguration()
    {
        var configuration = new Persistence.BasicModel.PlugInConfiguration();

        configuration.SetConfiguration(AuctionHouseRules.Default, null);
        var roundTripped = configuration.GetConfiguration<AuctionHouseRules>(null);

        Assert.That(roundTripped!.Goods.Select(goods => (goods.Group, goods.Number)), Is.EqualTo(AuctionHouseRules.Default.Goods.Select(goods => (goods.Group, goods.Number))));
        Assert.That(roundTripped.PostingFeeBasisPoints, Is.EqualTo(500));
        Assert.That(roundTripped.SaleFeeBasisPoints, Is.EqualTo(1_000));
        Assert.That(roundTripped.DurationHours, Is.EqualTo(new byte[] { 48 }));

        configuration.CustomConfiguration = """{ "SaleFeeBasisPoints": 800 }""";
        var withoutGoods = configuration.GetConfiguration<AuctionHouseRules>(null);
        Assert.That(withoutGoods!.SaleFeeBasisPoints, Is.EqualTo(800));
        Assert.That(withoutGoods.Goods, Has.Count.EqualTo(20));
    }
}
