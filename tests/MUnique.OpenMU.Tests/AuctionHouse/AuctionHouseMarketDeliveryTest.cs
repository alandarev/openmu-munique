// <copyright file="AuctionHouseMarketDeliveryTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests for the delivery to sellers after sales and expiry, the expiry itself and the notice at login.
/// </summary>
[TestFixture]
public class AuctionHouseMarketDeliveryTest
{
    private const byte Slot = AuctionHouseMarketTestHelper.FirstInventorySlot;

    private AuctionHouseMarketTestHelper _helper = null!;
    private ManualTimeProvider _time = null!;
    private GameContext _gameContext = null!;
    private AuctionHouseMarket _market = null!;
    private Player _seller = null!;
    private Player _buyer = null!;

    /// <summary>
    /// Creates a market with a controllable time, a seller who is in the game and a buyer.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._helper = new AuctionHouseMarketTestHelper();
        this._time = new ManualTimeProvider();
        this._gameContext = (GameContext)this._helper.CreateGameContext();
        this._market = new AuctionHouseMarket(this._helper.ContextProvider, this._helper.Configuration, new NullLogger<AuctionHouseMarket>(), this._time);
        this._gameContext.FeaturePlugIns.GetPlugIn<AuctionHouseFeaturePlugIn>()!.Market = this._market;
        this._market.RegisterGameContext(this._gameContext);
        this._seller = await this.CreateOnlinePlayerAsync(this._gameContext, "seller", 100_000_000).ConfigureAwait(false);
        this._buyer = await this.CreateOnlinePlayerAsync(this._gameContext, "buyer", 1_000_000_000).ConfigureAwait(false);
    }

    /// <summary>
    /// A seller at the broker receives the proceeds in the inventory Zen and the notice "sold, collected".
    /// </summary>
    [Test]
    public async Task SellerAtBrokerReceivesProceedsAsync()
    {
        var listingId = await this.ListItemAsync(10_000_000).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(this._seller).ConfigureAwait(false);

        await this.BuyAsync(listingId).ConfigureAwait(false);

        Assert.That(this._seller.Money, Is.EqualTo(100_000_000 - 500_000 + 9_000_000));
        Assert.That(await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false), Is.Empty);
        this.VerifyNotice(this._seller, AuctionHouseNoticeKind.SoldToInventory, 9_000_000, 0);
        this.VerifyNoNotice(this._buyer);
    }

    /// <summary>
    /// A seller elsewhere in the game gets the notice "sold, in the collection" and keeps the entry.
    /// </summary>
    [Test]
    public async Task SellerElsewhereGetsNoticeAsync()
    {
        var listingId = await this.ListItemAsync(10_000_000).ConfigureAwait(false);

        await this.BuyAsync(listingId).ConfigureAwait(false);

        Assert.That(this._seller.Money, Is.EqualTo(100_000_000 - 500_000));
        Assert.That((await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false)).Single().UncollectedMoney, Is.EqualTo(9_000_000));
        this.VerifyNotice(this._seller, AuctionHouseNoticeKind.Sold, 9_000_000, 1);
    }

    /// <summary>
    /// The notice of a sold item carries a copy of the item, so that the client can name it as it looks (e.g. excellent);
    /// the copy is not the item which went to the buyer. Goods notices carry none.
    /// </summary>
    [Test]
    public async Task SoldItemNoticeCarriesACopyOfTheItemAsync()
    {
        var listed = await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot, 7).ConfigureAwait(false);
        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, 10_000_000, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));

        await this.BuyAsync(outcome.ListingId).ConfigureAwait(false);

        this._seller.View<IShowAuctionNoticePlugIn>().Verify(
            v => v.ShowAuctionNoticeAsync(It.Is<AuctionHouseNotice>(n => n.Item != null
                && n.Item != listed
                && n.Item.Definition == this._helper.Kris
                && n.Item.Level == 7
                && n.Item.ItemOptions.Count == listed.ItemOptions.Count)),
            Times.Once);
    }

    /// <summary>
    /// The notice of sold goods carries no item.
    /// </summary>
    [Test]
    public async Task SoldGoodsNoticeCarriesNoItemAsync()
    {
        _ = await this.ListGoodsAsync(2).ConfigureAwait(false);
        var outcome = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 2, 2_000_000)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);

        this._seller.View<IShowAuctionNoticePlugIn>().Verify(v => v.ShowAuctionNoticeAsync(It.Is<AuctionHouseNotice>(n => n.Item == null)), Times.Once);
    }

    /// <summary>
    /// The notice of an expired item listing carries a copy of the item.
    /// </summary>
    [Test]
    public async Task ExpiredItemNoticeCarriesACopyOfTheItemAsync()
    {
        await this.ListItemAsync(10_000_000).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromHours(48));

        await this._market.ExpireDueListingsAsync(true).ConfigureAwait(false);
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);

        this._seller.View<IShowAuctionNoticePlugIn>().Verify(
            v => v.ShowAuctionNoticeAsync(It.Is<AuctionHouseNotice>(n => n.Kind == AuctionHouseNoticeKind.Expired && n.Item != null && n.Item.Definition == this._helper.Kris)),
            Times.Once);
    }

    /// <summary>
    /// An offline seller gets nothing now; the proceeds wait in the collection.
    /// </summary>
    [Test]
    public async Task OfflineSellerGetsNoNoticeAsync()
    {
        var listingId = await this.ListItemAsync(10_000_000).ConfigureAwait(false);
        this._gameContext.PlayersByCharacterName.TryRemove("seller", out _);

        await this.BuyAsync(listingId).ConfigureAwait(false);

        this.VerifyNoNotice(this._seller);
        Assert.That(await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// A seller at the broker near the Zen limit collects what fits; the rest stays in the collection, so the notice is
    /// "sold, in the collection".
    /// </summary>
    [Test]
    public async Task SellerAtBrokerNearZenLimitGetsPartialClaimAsync()
    {
        var listingId = await this.ListItemAsync(10_000_000).ConfigureAwait(false);
        this._seller.SelectedCharacter!.Inventory!.Money = int.MaxValue - 1_000;
        await AuctionHouseTestHelper.OpenBrokerAsync(this._seller).ConfigureAwait(false);

        await this.BuyAsync(listingId).ConfigureAwait(false);

        Assert.That(this._seller.Money, Is.EqualTo(int.MaxValue));
        Assert.That((await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false)).Single().UncollectedMoney, Is.EqualTo(9_000_000 - 1_000));
        this.VerifyNotice(this._seller, AuctionHouseNoticeKind.Sold, 9_000_000, 1);
    }

    /// <summary>
    /// A seller who is in another game context sharing the market is found.
    /// </summary>
    [Test]
    public async Task SellerOnAnotherGameContextIsFoundAsync()
    {
        var otherContext = (GameContext)this._helper.CreateGameContext();
        otherContext.FeaturePlugIns.GetPlugIn<AuctionHouseFeaturePlugIn>()!.Market = this._market;
        Assert.That(AuctionHouseMarkets.GetMarket(otherContext), Is.SameAs(this._market));
        var remoteSeller = await this.CreateOnlinePlayerAsync(otherContext, "remote", 100_000_000).ConfigureAwait(false);
        await this._helper.AddItemAsync(remoteSeller, this._helper.Kris, Slot).ConfigureAwait(false);
        var created = await this._market.CreateListingAsync(remoteSeller, new AuctionCreateListingOrder(Slot, 1, 1_000_000, 48, 0)).ConfigureAwait(false);

        await this.BuyAsync(created.ListingId, 1_000_000).ConfigureAwait(false);

        this.VerifyNotice(remoteSeller, AuctionHouseNoticeKind.Sold, 900_000, 1);
    }

    /// <summary>
    /// After the duration, the expiry moves the unsold units of a goods listing into an "expired" entry of the offline
    /// seller and removes the listing from the index; a second run expires nothing.
    /// </summary>
    [Test]
    public async Task ExpiryMovesUnsoldUnitsIntoCollectionAsync()
    {
        var listingId = await this.ListGoodsAsync(5).ConfigureAwait(false);
        await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 2, 2_000_000)).ConfigureAwait(false);
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);
        this._gameContext.PlayersByCharacterName.TryRemove("seller", out _);

        Assert.That(await this._market.ExpireDueListingsAsync(true).ConfigureAwait(false), Is.Zero, "not due yet");
        this._time.Advance(TimeSpan.FromHours(48));
        var expired = await this._market.ExpireDueListingsAsync(true).ConfigureAwait(false);
        var again = await this._market.ExpireDueListingsAsync(true).ConfigureAwait(false);

        Assert.That(expired, Is.EqualTo(1));
        Assert.That(again, Is.Zero);
        Assert.That((await this._helper.GetListingAsync(listingId).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Expired));
        Assert.That(this._market.Index.Get(listingId), Is.Null);
        var entries = await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false);
        var expiredEntry = entries.Single(entry => entry.Kind == AuctionHouseCollectionEntryKind.Expired);
        Assert.That(expiredEntry.Quantity, Is.EqualTo(3));
        Assert.That(expiredEntry.Escrow!.Items, Has.Count.EqualTo(3));
        Assert.That(entries.Single(entry => entry.Kind == AuctionHouseCollectionEntryKind.Proceeds).Quantity, Is.EqualTo(2));
    }

    /// <summary>
    /// A seller at the broker gets the expired units back into the inventory with the notice "expired, returned".
    /// </summary>
    [Test]
    public async Task ExpiryReturnsUnitsToSellerAtBrokerAsync()
    {
        await this.ListGoodsAsync(5).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(this._seller).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromHours(48));

        await this._market.ExpireDueListingsAsync(true).ConfigureAwait(false);
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);

        Assert.That(this._seller.Inventory!.Items.Count(), Is.EqualTo(5));
        this.VerifyNotice(this._seller, AuctionHouseNoticeKind.ExpiredToInventory, 0, 0);
    }

    /// <summary>
    /// Two markets on the same database (e.g. after a restart, or two processes) don't expire a listing twice.
    /// </summary>
    [Test]
    public async Task ConcurrentExpiryRunsExpireOnceAsync()
    {
        await this.ListGoodsAsync(5).ConfigureAwait(false);
        var secondMarket = new AuctionHouseMarket(this._helper.ContextProvider, this._helper.Configuration, new NullLogger<AuctionHouseMarket>(), this._time);
        this._time.Advance(TimeSpan.FromHours(48));

        var counts = await Task.WhenAll(
            this._market.ExpireDueListingsAsync().AsTask(),
            secondMarket.ExpireDueListingsAsync().AsTask()).ConfigureAwait(false);

        Assert.That(counts.Sum(), Is.EqualTo(1));
        Assert.That((await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false)).Count(entry => entry.Kind == AuctionHouseCollectionEntryKind.Expired), Is.EqualTo(1));
    }

    /// <summary>
    /// Without forcing, the expiry runs at most every <see cref="AuctionHouseMarket.ExpiryInterval"/>.
    /// </summary>
    [Test]
    public async Task ExpiryRunsAtMostEveryIntervalAsync()
    {
        var first = await this.ListGoodsAsync(2).ConfigureAwait(false);
        var second = await this.ListGoodsAsync(2).ConfigureAwait(false);
        (await this._helper.GetListingAsync(second).ConfigureAwait(false))!.ExpiresAt = this._time.GetUtcNow().UtcDateTime.AddHours(49);
        this._time.Advance(TimeSpan.FromHours(48));
        Assert.That(await this._market.ExpireDueListingsAsync().ConfigureAwait(false), Is.EqualTo(1));
        (await this._helper.GetListingAsync(second).ConfigureAwait(false))!.ExpiresAt = this._time.GetUtcNow().UtcDateTime;

        var tooEarly = await this._market.ExpireDueListingsAsync().ConfigureAwait(false);
        this._time.Advance(AuctionHouseMarket.ExpiryInterval);
        var afterInterval = await this._market.ExpireDueListingsAsync().ConfigureAwait(false);

        Assert.That(tooEarly, Is.Zero);
        Assert.That(afterInterval, Is.EqualTo(1));
        Assert.That((await this._helper.GetListingAsync(first).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Expired));
    }

    /// <summary>
    /// An expired listing can't be bought, also before the expiry ran; it isn't searched or quoted either.
    /// </summary>
    [Test]
    public async Task ExpiredListingCantBeBoughtAsync()
    {
        var listingId = await this.ListItemAsync(1_000_000).ConfigureAwait(false);
        await this.ListGoodsAsync(5).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromHours(48));

        var outcome = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 1_000_000)).ConfigureAwait(false);
        var goods = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 1, 1_000_000)).ConfigureAwait(false);
        var quote = await this._market.QuoteGoodsAsync(this._buyer, new AuctionGoodsQuoteQuery(this._helper.JewelOfBlessType, 0, 1)).ConfigureAwait(false);

        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.NotFound));
        Assert.That(goods.Result, Is.EqualTo(AuctionHouseResult.InsufficientStock));
        Assert.That(quote.FillableQuantity, Is.Zero);
        Assert.That(this._buyer.Money, Is.EqualTo(1_000_000_000));
    }

    /// <summary>
    /// The periodic task asks the market of its game context to expire.
    /// </summary>
    [Test]
    public async Task PeriodicTaskExpiresListingsAsync()
    {
        var listingId = await this.ListGoodsAsync(5).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromHours(48));
        var plugIn = new AuctionHouseExpiryPlugIn();
        plugIn.ForceStart();

        await plugIn.ExecuteTaskAsync(this._gameContext).ConfigureAwait(false);

        Assert.That((await this._helper.GetListingAsync(listingId).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Expired));
    }

    /// <summary>
    /// A character entering the game with waiting entries gets the "deliveries waiting" notice; without, none.
    /// </summary>
    [Test]
    public async Task LoginNoticeAnnouncesWaitingEntriesAsync()
    {
        var listingId = await this.ListItemAsync(1_000_000).ConfigureAwait(false);
        this._gameContext.PlayersByCharacterName.TryRemove("seller", out _);
        await this.BuyAsync(listingId, 1_000_000).ConfigureAwait(false);
        var plugIn = new AuctionHouseLoginNoticePlugIn();

        await plugIn.PlayerStateChangedAsync(this._seller, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(this._buyer, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(this._seller, PlayerState.EnteredWorld, PlayerState.NpcDialogOpened).ConfigureAwait(false);

        this._seller.View<IShowAuctionNoticePlugIn>().Verify(v => v.ShowAuctionNoticeAsync(new AuctionHouseNotice(AuctionHouseNoticeKind.DeliveriesWaiting, 0, 0, 0, 0, 1)), Times.Once);
        this.VerifyNoNotice(this._buyer);
    }

    /// <summary>
    /// The seller collects the proceeds by an own claim before the delivery runs: the delivery finds the entry gone and
    /// announces it as delivered, not as waiting.
    /// </summary>
    [Test]
    public async Task EntryCollectedByOwnClaimBeforeDeliveryIsAnnouncedAsDeliveredAsync()
    {
        var listingId = await this.ListItemAsync(10_000_000).ConfigureAwait(false);

        // While the seller's own request holds the seller's persistence section, the delivery waits for it.
        await this._seller.RunPersistenceExclusiveAsync(async () =>
        {
            var bought = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, 10_000_000)).ConfigureAwait(false);
            Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
            var claimed = await this._market.ClaimAsync(this._seller, new AuctionClaimOrder(null)).ConfigureAwait(false);
            Assert.That(claimed.ZenCollected, Is.EqualTo(9_000_000));
        }).ConfigureAwait(false);
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);

        this.VerifyNotice(this._seller, AuctionHouseNoticeKind.SoldToInventory, 9_000_000, 0);
        this._seller.View<IShowAuctionNoticePlugIn>().Verify(v => v.ShowAuctionNoticeAsync(It.Is<AuctionHouseNotice>(n => n.Kind == AuctionHouseNoticeKind.Sold)), Times.Never);
    }

    /// <summary>
    /// Two sales of one goods listing accumulate in one proceeds entry; the first delivery claims it completely, so the
    /// second one finds it gone: both are announced as delivered.
    /// </summary>
    [Test]
    public async Task SecondDeliveryOfAccumulatedProceedsIsAnnouncedAsDeliveredAsync()
    {
        await this.ListGoodsAsync(5).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(this._seller).ConfigureAwait(false);

        await this._seller.RunPersistenceExclusiveAsync(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var bought = await this._market.BuyGoodsAsync(this._buyer, new AuctionBuyGoodsOrder(this._helper.JewelOfBlessType, 0, 1, 1_000_000)).ConfigureAwait(false);
                Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
            }
        }).ConfigureAwait(false);
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);

        Assert.That(this._seller.Money, Is.EqualTo(100_000_000 - 250_000 + 1_800_000));
        this._seller.View<IShowAuctionNoticePlugIn>().Verify(v => v.ShowAuctionNoticeAsync(It.Is<AuctionHouseNotice>(n => n.Kind == AuctionHouseNoticeKind.SoldToInventory && n.PendingCollection == 0)), Times.Exactly(2));
    }

    /// <summary>
    /// A quarantined seller gets no delivery: the proceeds stay in the collection for the next login.
    /// </summary>
    [Test]
    public async Task QuarantinedSellerGetsNoDeliveryAsync()
    {
        var listingId = await this.ListItemAsync(10_000_000).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(this._seller).ConfigureAwait(false);
        this._seller.PersistenceContext.Quarantine();
        var money = this._seller.Money;

        await this.BuyAsync(listingId).ConfigureAwait(false);

        Assert.That(this._seller.Money, Is.EqualTo(money));
        Assert.That((await this._helper.GetEntriesAsync(this._seller.SelectedCharacter!).ConfigureAwait(false)).Single().UncollectedMoney, Is.EqualTo(9_000_000));
        this.VerifyNoNotice(this._seller);
    }

    /// <summary>
    /// Own listings and the seller summary leave out listings whose duration ended, before the expiry ran.
    /// </summary>
    [Test]
    public async Task OwnListingsAndSummaryExcludeExpiredListingsAsync()
    {
        await this.ListItemAsync(1_000_000).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromHours(24));
        await this.ListGoodsAsync(2).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromHours(25));

        var own = await this._market.GetOwnListingsAsync(this._seller).ConfigureAwait(false);
        var summary = await this._market.GetSellerSummaryAsync(this._seller).ConfigureAwait(false);
        var check = await this._market.CheckSellAsync(this._seller, new AuctionSellCheckQuery(Slot)).ConfigureAwait(false);

        Assert.That(own.Listings.Select(row => row.Kind), Is.EqualTo(new[] { AuctionHouseListingKind.Goods }));
        Assert.That(summary.ActiveListings, Is.EqualTo(1));
        Assert.That(check.ActiveListings, Is.EqualTo(1));
    }

    /// <summary>
    /// A run which passed the deadline check, but acquired the run gate only after another run of a sharing game
    /// context finished, checks the deadline again and doesn't run.
    /// </summary>
    [Test]
    public async Task ExpiryRechecksDeadlineAfterAcquiringGateAsync()
    {
        var first = await this.ListGoodsAsync(2).ConfigureAwait(false);
        var second = await this.ListItemAsync(1_000_000).ConfigureAwait(false);
        (await this._helper.GetListingAsync(second).ConfigureAwait(false))!.ExpiresAt = this._time.GetUtcNow().UtcDateTime.AddHours(72);
        this._time.Advance(TimeSpan.FromHours(48));
        var pausedAtGate = new TaskCompletionSource();
        var releaseGate = new TaskCompletionSource();
        var calls = 0;
        this._market.BeforeExpiryGate = async () =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                pausedAtGate.SetResult();
                await releaseGate.Task.ConfigureAwait(false);
            }
        };

        var paused = this._market.ExpireDueListingsAsync().AsTask();
        await pausedAtGate.Task.ConfigureAwait(false);
        var other = await this._market.ExpireDueListingsAsync().ConfigureAwait(false);
        (await this._helper.GetListingAsync(second).ConfigureAwait(false))!.ExpiresAt = this._time.GetUtcNow().UtcDateTime;
        releaseGate.SetResult();
        var late = await paused.ConfigureAwait(false);

        Assert.That(other, Is.EqualTo(1));
        Assert.That(late, Is.Zero, "the late run must not run within the interval");
        Assert.That((await this._helper.GetListingAsync(first).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Expired));
        Assert.That((await this._helper.GetListingAsync(second).ConfigureAwait(false))!.State, Is.EqualTo(AuctionListingState.Active));
    }

    private async ValueTask<Player> CreateOnlinePlayerAsync(GameContext gameContext, string name, int money)
    {
        var player = await this._helper.CreatePlayerAsync(gameContext, name, money).ConfigureAwait(false);
        gameContext.PlayersByCharacterName[name] = player;
        return player;
    }

    private async ValueTask<Guid> ListItemAsync(uint price)
    {
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);
        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, price, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        return outcome.ListingId;
    }

    private async ValueTask<Guid> ListGoodsAsync(int quantity)
    {
        await this._helper.AddItemsAsync(this._seller, this._helper.JewelOfBless, quantity, Slot + 8).ConfigureAwait(false);
        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot + 8, (uint)quantity, 1_000_000, 48, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        return outcome.ListingId;
    }

    private async ValueTask BuyAsync(Guid listingId, uint price = 10_000_000)
    {
        var outcome = await this._market.BuyListingAsync(this._buyer, new AuctionBuyListingOrder(listingId, price)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success));
        await this._market.WaitForPendingDeliveriesAsync().ConfigureAwait(false);
    }

    private void VerifyNotice(Player player, AuctionHouseNoticeKind kind, uint net, ushort pending)
    {
        player.View<IShowAuctionNoticePlugIn>().Verify(v => v.ShowAuctionNoticeAsync(It.Is<AuctionHouseNotice>(n => n.Kind == kind && n.Net == net && n.PendingCollection == pending)), Times.Once);
    }

    private void VerifyNoNotice(Player player)
    {
        player.View<IShowAuctionNoticePlugIn>().Verify(v => v.ShowAuctionNoticeAsync(It.IsAny<AuctionHouseNotice>()), Times.Never);
    }

    /// <summary>
    /// A time provider whose time is advanced by the test.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this._now;

        public void Advance(TimeSpan duration) => this._now += duration;
    }
}
