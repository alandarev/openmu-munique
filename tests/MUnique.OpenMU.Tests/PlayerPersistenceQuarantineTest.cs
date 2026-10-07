// <copyright file="PlayerPersistenceQuarantineTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.GameLogic.PlayerActions.Trade;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that nothing of a player whose persistence is quarantined (<see cref="PlayerPersistenceQuarantine"/>) moves
/// anywhere while its disconnection is still pending.
/// </summary>
[TestFixture]
public class PlayerPersistenceQuarantineTest
{
    /// <summary>
    /// Quarantining closes the personal store, and the progress of the player isn't saved anymore.
    /// </summary>
    [Test]
    public async Task QuarantineClosesStoreAndRefusesSavesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.ShopStorage!.StoreOpen = true;

        await PlayerPersistenceQuarantine.QuarantineAsync(player).ConfigureAwait(false);

        Assert.That(player.ShopStorage.StoreOpen, Is.False);
        Assert.That(player.PersistenceContext.IsQuarantined, Is.True);
        Assert.That(await player.SaveProgressAsync().ConfigureAwait(false), Is.False);
    }

    /// <summary>
    /// A store purchase from a quarantined seller, whose store is still open (disconnection pending), changes nothing.
    /// </summary>
    [Test]
    public async Task StorePurchaseFromQuarantinedSellerChangesNothingAsync()
    {
        var seller = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var buyer = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        buyer.Money = 1_000_000;
        var item = CreateItem();
        item.StorePrice = 100;
        var store = seller.ShopStorage!;
        await store.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false);
        store.StoreOpen = true;
        seller.PersistenceContext.Quarantine();

        await new BuyRequestAction().BuyItemAsync(buyer, seller, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.That(store.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.SameAs(item));
        Assert.That(buyer.Inventory!.Items, Does.Not.Contain(item));
        Assert.That(buyer.Money, Is.EqualTo(1_000_000));
        Assert.That(seller.Money, Is.Zero);
    }

    /// <summary>
    /// A trade with a quarantined partner isn't completed; the items stay with their owners.
    /// </summary>
    /// <param name="quarantineSecond">If set to <c>true</c>, the partner who accepts last is quarantined; otherwise the first.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task TradeWithQuarantinedPartnerChangesNothingAsync(bool quarantineSecond)
    {
        var trader1 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var trader2 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var item = CreateItem();
        await trader1.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);
        await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        await new MoveItemAction().MoveItemAsync(trader1, 20, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        Assert.That(trader1.TemporaryStorage!.Items, Does.Contain(item));
        (quarantineSecond ? trader2 : trader1).PersistenceContext.Quarantine();

        var tradeButtonAction = new TradeButtonAction();
        await tradeButtonAction.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonAction.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);

        Assert.That(trader2.Inventory!.Items, Does.Not.Contain(item));
        Assert.That(trader1.Inventory.Items.Concat(trader1.TemporaryStorage.Items), Does.Contain(item));
    }

    /// <summary>
    /// The packet funnel drops every packet of a quarantined player, e.g. a drop request, before it reaches a handler.
    /// </summary>
    [Test]
    public async Task PacketsOfQuarantinedPlayerAreDroppedAsync()
    {
        var handler = new CountingDropHandlerPlugIn();
        var (player, receive) = CreateRemotePlayer();
        player.MainPacketHandler.AddPlugIn(handler, true);
        var dropRequest = new byte[] { 0xC1, 0x06, CountingDropHandlerPlugIn.DropKey, 0x10, 0x20, 0x0C };

        await receive(new ReadOnlySequence<byte>(dropRequest)).ConfigureAwait(false);
        player.PersistenceContext.Quarantine();
        await receive(new ReadOnlySequence<byte>(dropRequest)).ConfigureAwait(false);
        await receive(new ReadOnlySequence<byte>(dropRequest)).ConfigureAwait(false);

        Assert.That(handler.HandledPackets, Is.EqualTo(1));
    }

    private static Item CreateItem()
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Object.Definition = new ItemDefinition { Width = 1, Height = 1, Durability = 10 };
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        return item.Object;
    }

    private static (RemotePlayer Player, AsyncEventHandler<ReadOnlySequence<byte>> Receive) CreateRemotePlayer()
    {
        var manager = new PlugInManager(null, new NullLoggerFactory(), null, null);
        var gameContext = new Mock<IGameServerContext>();
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(new InMemoryPersistenceContextProvider());
        gameContext.Setup(c => c.Configuration).Returns(new GameConfiguration());
        gameContext.Setup(c => c.PlugInManager).Returns(manager);
        gameContext.Setup(c => c.LoggerFactory).Returns(new NullLoggerFactory());
        AsyncEventHandler<ReadOnlySequence<byte>>? receive = null;
        var connection = new Mock<IConnection>();
        connection.SetupAdd(c => c.PacketReceived += It.IsAny<AsyncEventHandler<ReadOnlySequence<byte>>>())
            .Callback<AsyncEventHandler<ReadOnlySequence<byte>>>(h => receive = h);
        var player = new RemotePlayer(gameContext.Object, connection.Object, default);
        return (player, receive ?? throw new InvalidOperationException("The player didn't subscribe to the connection."));
    }

    /// <summary>
    /// A packet handler which counts the packets it gets, registered for the key of the drop request.
    /// </summary>
    private sealed class CountingDropHandlerPlugIn : IPacketHandlerPlugIn
    {
        /// <summary>
        /// The key of the drop item request.
        /// </summary>
        public const byte DropKey = 0x23;

        public int HandledPackets { get; private set; }

        public bool IsEncryptionExpected => false;

        public byte Key => DropKey;

        public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
        {
            this.HandledPackets++;
            return ValueTask.CompletedTask;
        }
    }
}
