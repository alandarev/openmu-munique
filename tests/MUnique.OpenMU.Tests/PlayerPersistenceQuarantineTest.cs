// <copyright file="PlayerPersistenceQuarantineTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Buffers;
using System.IO;
using System.IO.Pipelines;
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
using MUnique.OpenMU.GameServer.MessageHandler.Items;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Pathfinding;
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
    /// The longest time a test waits for a barrier or a task before it fails.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

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
        trader1.Money = 10_000;
        trader2.Money = 20_000;
        await trader1.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);
        await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        await new MoveItemAction().MoveItemAsync(trader1, 20, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        await new TradeMoneyAction().TradeMoneyAsync(trader1, 1_000).ConfigureAwait(false);
        await new TradeMoneyAction().TradeMoneyAsync(trader2, 2_000).ConfigureAwait(false);
        Assert.That(trader1.TemporaryStorage!.Items, Does.Contain(item));
        Assert.That(trader1.TradingMoney + trader2.TradingMoney, Is.EqualTo(3_000), "the trade holds Zen of both");
        (quarantineSecond ? trader2 : trader1).PersistenceContext.Quarantine();

        var tradeButtonAction = new TradeButtonAction();
        await tradeButtonAction.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonAction.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);

        Assert.That(trader2.Inventory!.Items, Does.Not.Contain(item));
        Assert.That(trader1.Inventory.Items.Concat(trader1.TemporaryStorage.Items), Does.Contain(item));
        Assert.That(trader1.Money + trader1.TradingMoney, Is.EqualTo(10_000), "the Zen of the first trader stays with it");
        Assert.That(trader2.Money + trader2.TradingMoney, Is.EqualTo(20_000), "the Zen of the second trader stays with it");
    }

    /// <summary>
    /// A drop request which is queued behind the persistence section that quarantines the player is dropped, while the
    /// disconnection is still pending: the item stays in the inventory and isn't on the map. A drop before the quarantine
    /// shows that the same request drops an item otherwise.
    /// </summary>
    [Test]
    public async Task DropQueuedBehindQuarantineChangesNothingAsync()
    {
        var (player, receive) = await CreateRemotePlayerAsync().ConfigureAwait(false);
        var kept = await AddItemAsync(player, 12).ConfigureAwait(false);
        var dropped = await AddItemAsync(player, 13).ConfigureAwait(false);
        var map = player.CurrentMap!;

        await receive(DropRequest(13)).AsTask().WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(player.Inventory!.Items, Does.Not.Contain(dropped), "the drop works before the quarantine");
        Assert.That(map.GetDropsInRange(new Point(100, 100), 3).Select(drop => (drop as DroppedItem)?.Item), Does.Contain(dropped));

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var quarantining = player.RunPersistenceExclusiveAsync(async () =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(Timeout).ConfigureAwait(false);
            await PlayerPersistenceQuarantine.QuarantineAsync(player, () => disconnectGate.Task).ConfigureAwait(false);
        }).AsTask();
        await entered.Task.WaitAsync(Timeout).ConfigureAwait(false);

        // Received while the quarantining section holds the persistence lock: the request waits behind it.
        var queuedDrop = receive(DropRequest(12)).AsTask();
        Assert.That(queuedDrop.IsCompleted, Is.False, "the drop request waits for the persistence section");
        release.SetResult();
        await quarantining.WaitAsync(Timeout).ConfigureAwait(false);
        await queuedDrop.WaitAsync(Timeout).ConfigureAwait(false);

        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld), "the disconnection is still pending");
        Assert.That(player.Inventory.GetItem(12), Is.SameAs(kept));
        Assert.That(map.GetDropsInRange(new Point(100, 100), 3).Select(drop => (drop as DroppedItem)?.Item), Does.Not.Contain(kept));
        disconnectGate.SetResult();
    }

    private static ReadOnlySequence<byte> DropRequest(byte slot) => new([0xC3, 0x06, 0x23, 100, 100, slot]);

    private static async ValueTask<Item> AddItemAsync(Player player, byte slot)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = new Persistence.BasicModel.ItemDefinition { Width = 1, Height = 1, Durability = 1 };
        item.Durability = 1;
        Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True);
        return item;
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

    private static async ValueTask<(RemotePlayer Player, AsyncEventHandler<ReadOnlySequence<byte>> Receive)> CreateRemotePlayerAsync()
    {
        var gameConfiguration = new Persistence.BasicModel.GameConfiguration();

        // Dropped items stay on the map during the test.
        gameConfiguration.ItemDropDuration = TimeSpan.FromMinutes(10);
        gameConfiguration.Maps.Add(new Persistence.BasicModel.GameMapDefinition { Number = 0, TerrainData = new byte[ushort.MaxValue + 3] });
        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameServerContext(
            new Persistence.BasicModel.GameServerDefinition
            {
                GameConfiguration = gameConfiguration,
                ServerConfiguration = new Persistence.BasicModel.GameServerConfiguration(),
            },
            new Mock<IGuildServer>().Object,
            new Mock<IEventPublisher>().Object,
            new Mock<ILoginServer>().Object,
            new Mock<IFriendServer>().Object,
            new InMemoryPersistenceContextProvider(),
            mapInitializer,
            new NullLoggerFactory(),
            new PlugInManager([], new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;

        AsyncEventHandler<ReadOnlySequence<byte>>? receive = null;
        var connection = new Mock<IConnection>();
        connection.SetupGet(c => c.Connected).Returns(true);
        connection.SetupGet(c => c.Output).Returns(PipeWriter.Create(new MemoryStream()));
        connection.SetupGet(c => c.OutputLock).Returns(new Nito.AsyncEx.AsyncLock());
        connection.SetupAdd(c => c.PacketReceived += It.IsAny<AsyncEventHandler<ReadOnlySequence<byte>>>())
            .Callback<AsyncEventHandler<ReadOnlySequence<byte>>>(h => receive = h);
        var player = new RemotePlayer(gameContext, connection.Object, default);
        player.MainPacketHandler.AddPlugIn(new DropItemHandlerPlugIn(), true);
        await PlayerTestHelper.InitializePlayerAsync(player).ConfigureAwait(false);
        return (player, receive ?? throw new InvalidOperationException("The player didn't subscribe to the connection."));
    }
}
