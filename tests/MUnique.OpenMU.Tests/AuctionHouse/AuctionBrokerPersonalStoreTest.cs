// <copyright file="AuctionBrokerPersonalStoreTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.GameLogic.Views.PlayerShop;

/// <summary>
/// Tests that a player at an auction house broker has no open personal store: talking to the broker closes it, and it
/// can't be opened while the broker is open.
/// </summary>
[TestFixture]
public class AuctionBrokerPersonalStoreTest
{
    private const short ZyroNumber = 568;

    /// <summary>
    /// Talking to Zyro with an open personal store closes the store, tells the player about it, and keeps the prices
    /// of the store items, so that the store can be opened again later without setting them again.
    /// </summary>
    [Test]
    public async Task TalkingToBrokerClosesOpenStoreAsync()
    {
        var seller = await AuctionHouseTestHelper.CreatePlayerAsync(new FakeAuctionHouseMarket()).ConfigureAwait(false);
        var item = await AddStoreItemAsync(seller).ConfigureAwait(false);
        await new OpenStoreAction().OpenStoreAsync(seller, "store").ConfigureAwait(false);
        Assert.That(seller.ShopStorage!.StoreOpen, Is.True);

        await TalkToZyroAsync(seller).ConfigureAwait(false);

        Assert.That(AuctionBrokerSession.IsBrokerOpen(seller), Is.True);
        Assert.That(seller.ShopStorage.StoreOpen, Is.False);
        seller.View<IPlayerShopClosedPlugIn>().Verify(v => v.PlayerShopClosedAsync(seller), Times.Once);
        Assert.That(item.StorePrice, Is.EqualTo(100), "the price stays for the next opening");
    }

    /// <summary>
    /// Opening the personal store while the broker is open is refused and changes nothing.
    /// </summary>
    [Test]
    public async Task OpeningStoreAtBrokerIsRefusedAsync()
    {
        var seller = await AuctionHouseTestHelper.CreatePlayerAsync(new FakeAuctionHouseMarket()).ConfigureAwait(false);
        await AddStoreItemAsync(seller).ConfigureAwait(false);
        seller.SelectedCharacter!.StoreName = "before";
        await TalkToZyroAsync(seller).ConfigureAwait(false);

        await new OpenStoreAction().OpenStoreAsync(seller, "store").ConfigureAwait(false);

        Assert.That(seller.ShopStorage!.StoreOpen, Is.False);
        Assert.That(seller.SelectedCharacter.StoreName, Is.EqualTo("before"));
        seller.View<IPlayerShopOpenedPlugIn>().Verify(v => v.PlayerShopOpenedAsync(It.IsAny<Player>()), Times.Never);
    }

    /// <summary>
    /// After the broker closed the store, a purchase from it is refused and moves nothing.
    /// </summary>
    [Test]
    public async Task PurchaseFromStoreAfterBrokerOpenedIsRefusedAsync()
    {
        var seller = await AuctionHouseTestHelper.CreatePlayerAsync(new FakeAuctionHouseMarket()).ConfigureAwait(false);
        var buyer = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        buyer.Money = 1_000_000;
        var item = await AddStoreItemAsync(seller).ConfigureAwait(false);
        await new OpenStoreAction().OpenStoreAsync(seller, "store").ConfigureAwait(false);
        await TalkToZyroAsync(seller).ConfigureAwait(false);

        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.That(seller.ShopStorage!.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.SameAs(item));
        Assert.That(buyer.Inventory!.Items, Does.Not.Contain(item));
        Assert.That(buyer.Money, Is.EqualTo(1_000_000));
        Assert.That(seller.Money, Is.Zero);
    }

    private static async ValueTask TalkToZyroAsync(Player player)
    {
        var zyro = new NonPlayerCharacter(null!, new Persistence.BasicModel.MonsterDefinition { Number = ZyroNumber, NpcWindow = NpcWindow.AuctionHouse }, null!);
        await new TalkNpcAction().TalkToNpcAsync(player, zyro).ConfigureAwait(false);
    }

    private static async ValueTask<Item> AddStoreItemAsync(Player player)
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Object.Definition = new ItemDefinition { Width = 1, Height = 1, Durability = 10 };
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        item.Object.StorePrice = 100;
        Assert.That(await player.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item.Object).ConfigureAwait(false), Is.True);
        return item.Object;
    }
}
