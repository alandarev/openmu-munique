// <copyright file="AuctionHouseMarketClaimTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests for claiming the collection of the <see cref="AuctionHouseMarket"/>.
/// </summary>
[TestFixture]
public class AuctionHouseMarketClaimTest
{
    private AuctionHouseMarketTestHelper _helper = null!;
    private AuctionHouseMarket _market = null!;
    private Player _player = null!;

    /// <summary>
    /// Creates a market and a player.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._helper = new AuctionHouseMarketTestHelper();
        var gameContext = this._helper.CreateGameContext();
        this._market = AuctionHouseMarkets.Get(gameContext);
        this._player = await this._helper.CreatePlayerAsync(gameContext, "player").ConfigureAwait(false);
    }

    /// <summary>
    /// The spec's "wallet near its limit": 980,000,000 Zen proceeds are claimed with 1,500,000,000 Zen in the inventory
    /// and a limit of <see cref="int.MaxValue"/>; 647,483,647 are collected, 332,516,353 remain, the answer is partial.
    /// </summary>
    [Test]
    public async Task ClaimStopsAtZenLimitAsync()
    {
        this._player.SelectedCharacter!.Inventory!.Money = 1_500_000_000;
        var entry = this.CreateEntry(AuctionHouseCollectionEntryKind.Proceeds, 0, 980_000_000);

        var outcome = await this._market.ClaimAsync(this._player, new AuctionClaimOrder(null)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Partial));
        Assert.That(outcome.ZenCollected, Is.EqualTo(647_483_647));
        Assert.That(outcome.KeptBack, Is.EqualTo(AuctionHouseKeptBack.ZenLimit));
        Assert.That(outcome.CompletedEntries, Is.Zero);
        Assert.That(outcome.RemainingEntries, Is.EqualTo(1));
        Assert.That(this._player.Money, Is.EqualTo(int.MaxValue));
        Assert.That(entry.UncollectedMoney, Is.EqualTo(332_516_353));
        Assert.That(entry.Gross, Is.EqualTo(1_000_000_000), "the gross of partly claimed proceeds stays");
    }

    /// <summary>
    /// Items which don't fit stay in the entry with their quantity; after making room, the rest is claimed and the entry
    /// is completed.
    /// </summary>
    [Test]
    public async Task ClaimWithFullInventoryKeepsItemsBackAsync()
    {
        var entry = this.CreateEntry(AuctionHouseCollectionEntryKind.Purchase, 5, 0);
        await this._helper.FillInventoryAsync(this._player).ConfigureAwait(false);
        var freed = this._player.Inventory!.Items.Take(2).ToList();
        foreach (var item in freed)
        {
            await this._player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        var partial = await this._market.ClaimAsync(this._player, new AuctionClaimOrder(entry.GetId())).ConfigureAwait(false);

        Assert.That(partial.Result, Is.EqualTo(AuctionHouseResult.Partial));
        Assert.That(partial.KeptBack, Is.EqualTo(AuctionHouseKeptBack.InventorySpace));
        Assert.That(partial.ItemsMoved, Is.EqualTo(2));
        Assert.That(partial.CompletedEntries, Is.Zero);
        Assert.That(partial.RemainingEntries, Is.EqualTo(1));
        Assert.That(entry.Quantity, Is.EqualTo(3));
        Assert.That(entry.Escrow!.Items, Has.Count.EqualTo(3));

        foreach (var item in this._player.Inventory.Items.Where(item => item.Definition == this._helper.JewelOfSoul).Take(3).ToList())
        {
            await this._player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        var complete = await this._market.ClaimAsync(this._player, new AuctionClaimOrder(entry.GetId())).ConfigureAwait(false);

        Assert.That(complete.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(complete.ItemsMoved, Is.EqualTo(3));
        Assert.That(complete.CompletedEntries, Is.EqualTo(1));
        Assert.That(complete.RemainingEntries, Is.Zero);
        Assert.That(await this._helper.GetEntriesAsync(this._player.SelectedCharacter!).ConfigureAwait(false), Is.Empty);
        Assert.That(this._player.Inventory.Items.Count(item => item.Definition == this._helper.JewelOfBless), Is.EqualTo(5));
    }

    /// <summary>
    /// Claiming all entries completes every entry which fits and counts the remaining ones.
    /// </summary>
    [Test]
    public async Task ClaimAllCompletesEveryEntryAsync()
    {
        this.CreateEntry(AuctionHouseCollectionEntryKind.Proceeds, 0, 1_000_000);
        this.CreateEntry(AuctionHouseCollectionEntryKind.Expired, 2, 0);
        this.CreateEntry(AuctionHouseCollectionEntryKind.Cancelled, 1, 50_000);

        var outcome = await this._market.ClaimAsync(this._player, new AuctionClaimOrder(null)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.CompletedEntries, Is.EqualTo(3));
        Assert.That(outcome.ItemsMoved, Is.EqualTo(3));
        Assert.That(outcome.ZenCollected, Is.EqualTo(1_050_000));
        Assert.That(outcome.RemainingEntries, Is.Zero);
        Assert.That(outcome.KeptBack, Is.EqualTo(AuctionHouseKeptBack.None));
        Assert.That(this._player.Money, Is.EqualTo(1_050_000));
    }

    /// <summary>
    /// Another character's entry and an unknown entry are not found.
    /// </summary>
    [Test]
    public async Task ClaimingForeignEntryIsNotFoundAsync()
    {
        var other = await this._helper.CreatePlayerAsync(this._player.GameContext, "other").ConfigureAwait(false);
        var foreign = this.CreateEntry(AuctionHouseCollectionEntryKind.Proceeds, 0, 1_000, other.SelectedCharacter!);

        var outcome = await this._market.ClaimAsync(this._player, new AuctionClaimOrder(foreign.GetId())).ConfigureAwait(false);
        var unknown = await this._market.ClaimAsync(this._player, new AuctionClaimOrder(Guid.NewGuid())).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(unknown.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(foreign.UncollectedMoney, Is.EqualTo(1_000));
        Assert.That(this._player.Money, Is.Zero);
    }

    /// <summary>
    /// The seller's proceeds of a purchase are claimed by the seller.
    /// </summary>
    [Test]
    public async Task SellerClaimsProceedsOfPurchaseAsync()
    {
        var seller = await this._helper.CreatePlayerAsync(this._player.GameContext, "seller", 100_000_000).ConfigureAwait(false);
        await this._helper.AddItemAsync(seller, this._helper.Kris, AuctionHouseMarketTestHelper.FirstInventorySlot).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(seller, new AuctionCreateListingOrder(AuctionHouseMarketTestHelper.FirstInventorySlot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        this._player.SelectedCharacter!.Inventory!.Money = 10_000_000;
        await this._market.BuyListingAsync(this._player, new AuctionBuyListingOrder(created.ListingId, 10_000_000)).ConfigureAwait(false);

        var outcome = await this._market.ClaimAsync(seller, new AuctionClaimOrder(null)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(outcome.ZenCollected, Is.EqualTo(9_000_000));
        Assert.That(seller.Money, Is.EqualTo(100_000_000 - 500_000 + 9_000_000));
        Assert.That(this._player.Money, Is.Zero);
    }

    private AuctionCollectionEntry CreateEntry(AuctionHouseCollectionEntryKind kind, int itemCount, long money, Character? owner = null)
    {
        var context = this._helper.ContextProvider.CreateNewContext();
        var entry = context.CreateNew<AuctionCollectionEntry>();
        entry.OwnerCharacterId = (owner ?? this._player.SelectedCharacter!).GetId();
        entry.Kind = kind;
        entry.ItemGroup = this._helper.JewelOfBless.Group;
        entry.ItemNumber = this._helper.JewelOfBless.Number;
        entry.Quantity = kind == AuctionHouseCollectionEntryKind.Proceeds ? 1 : itemCount;
        entry.Gross = kind == AuctionHouseCollectionEntryKind.Proceeds ? money * 100 / 98 : 0;
        entry.UncollectedMoney = money;
        entry.UpdatedAt = DateTime.UtcNow;
        if (kind != AuctionHouseCollectionEntryKind.Proceeds)
        {
            entry.Escrow = context.CreateNew<ItemStorage>();
            for (var i = 0; i < itemCount; i++)
            {
                var item = context.CreateNew<Item>();
                item.Definition = this._helper.JewelOfBless;
                item.Durability = 1;
                entry.Escrow.Items.Add(item);
            }
        }

        return entry;
    }
}
