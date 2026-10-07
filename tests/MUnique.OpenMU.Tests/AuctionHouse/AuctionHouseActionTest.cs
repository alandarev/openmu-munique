// <copyright file="AuctionHouseActionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.PlayerActions.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Tests for the <see cref="AuctionHouseAction"/>: opening and closing the broker, gating, pacing and follow-ups.
/// </summary>
[TestFixture]
public class AuctionHouseActionTest
{
    private static readonly AuctionSearchQuery SearchQuery = new(0, AuctionHouseSortOrder.PriceAscending, AuctionHouseEquipmentSlots.None, 0, AuctionHouseClassLine.Any, AuctionHouseSearchOptions.None, AuctionHouseItemFamily.Any, 0, 15, 0, 0, 0, 0, [], []);

    private static readonly byte[] NoDurations = [];

    private readonly AuctionHouseAction _action = new();

    /// <summary>
    /// Talking to an auction house NPC opens the broker with the configured rules and the counts of the player
    /// from the market, instead of the NPC window response.
    /// </summary>
    [Test]
    public async Task TalkingToAuctionHouseNpcOpensBrokerAsync()
    {
        var market = new FakeAuctionHouseMarket { SellerSummary = new AuctionSellerSummary(3, 2) };
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);

        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.NpcDialogOpened));
        Assert.That(AuctionBrokerSession.IsBrokerOpen(player), Is.True);
        var expected = AuctionBrokerState.Create(AuctionHouseTestHelper.CreateRules(), int.MaxValue, new AuctionSellerSummary(3, 2));
        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseOpenedAsync(It.Is<AuctionBrokerState>(s => IsEqual(s, expected))), Times.Once);
        player.View<IOpenNpcWindowPlugIn>().Verify(v => v.OpenNpcWindowAsync(It.IsAny<NpcWindow>()), Times.Never);
    }

    /// <summary>
    /// Without an active auction house feature, the broker opens with the default rules and zero counts.
    /// </summary>
    [Test]
    public async Task BrokerOpensWithDefaultRulesWithoutFeatureAsync()
    {
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(null, activateFeature: false).ConfigureAwait(false);

        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        var expected = AuctionBrokerState.Create(AuctionHouseRules.Default, int.MaxValue, AuctionSellerSummary.None);
        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseOpenedAsync(It.Is<AuctionBrokerState>(s => IsEqual(s, expected))), Times.Once);
    }

    /// <summary>
    /// A request without an open broker is answered with <see cref="AuctionHouseResult.NotOpen"/> and no rows.
    /// </summary>
    [Test]
    public async Task RequestWithoutOpenBrokerIsAnsweredNotOpenAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);

        await this._action.SearchAsync(player, 4711, SearchQuery).ConfigureAwait(false);

        player.View<IShowAuctionSearchResultPlugIn>().Verify(v => v.ShowAuctionSearchResultAsync(4711, It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.NotOpen && p.Listings.Count == 0)), Times.Once);
        Assert.That(market.Calls, Is.Empty);
    }

    /// <summary>
    /// A request to another NPC's dialog is answered with <see cref="AuctionHouseResult.NotOpen"/>.
    /// </summary>
    [Test]
    public async Task RequestWithOtherNpcDialogIsAnsweredNotOpenAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false);
        player.OpenedNpc = new NonPlayerCharacter(null!, new Persistence.BasicModel.MonsterDefinition { NpcWindow = NpcWindow.VaultStorage }, null!);

        await this._action.GetOwnListingsAsync(player, 1).ConfigureAwait(false);

        player.View<IShowAuctionOwnListingsPlugIn>().Verify(v => v.ShowAuctionOwnListingsAsync(1, It.Is<AuctionOwnListingList>(l => l.Result == AuctionHouseResult.NotOpen)), Times.Once);
        Assert.That(market.Calls, Is.Empty);
    }

    /// <summary>
    /// Without an active feature, the broker opens and every request is answered with <see cref="AuctionHouseResult.Unavailable"/>.
    /// With an active feature, the shared market of the game context answers (see <see cref="AuctionHouseMarketSharingTest"/>).
    /// </summary>
    [Test]
    public async Task RequestWithoutFeatureIsAnsweredUnavailableAsync()
    {
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(null, activateFeature: false).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.SearchAsync(player, 7, SearchQuery).ConfigureAwait(false);
        await this._action.ClaimAsync(player, 8, new AuctionClaimOrder(null)).ConfigureAwait(false);

        player.View<IShowAuctionSearchResultPlugIn>().Verify(v => v.ShowAuctionSearchResultAsync(7, It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.Unavailable && p.Listings.Count == 0)), Times.Once);
        player.View<IShowAuctionClaimResultPlugIn>().Verify(v => v.ShowAuctionClaimResultAsync(8, It.Is<AuctionClaimOutcome>(o => o.Result == AuctionHouseResult.Unavailable)), Times.Once);
    }

    /// <summary>
    /// A quarantined player, whose transaction ended with an unknown outcome, gets every request refused with
    /// <see cref="AuctionHouseResult.InternalError"/> without reaching the market.
    /// </summary>
    [Test]
    public async Task QuarantinedPlayerIsRefusedAsync()
    {
        var market = new Mock<IAuctionHouseMarket>(MockBehavior.Strict);
        market.Setup(m => m.GetSellerSummaryAsync(It.IsAny<Player>())).ReturnsAsync(AuctionSellerSummary.None);
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market.Object).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);
        player.PersistenceContext.Quarantine();

        await this._action.ClaimAsync(player, 8, new AuctionClaimOrder(null)).ConfigureAwait(false);

        player.View<IShowAuctionClaimResultPlugIn>().Verify(v => v.ShowAuctionClaimResultAsync(8, It.Is<AuctionClaimOutcome>(o => o.Result == AuctionHouseResult.InternalError)), Times.Once);
        market.Verify(m => m.ClaimAsync(It.IsAny<Player>(), It.IsAny<AuctionClaimOrder>()), Times.Never);
    }

    /// <summary>
    /// Requests above the configured rate are answered with <see cref="AuctionHouseResult.Busy"/> without reaching the market.
    /// </summary>
    [Test]
    public async Task RequestAboveRateIsAnsweredBusyAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market, AuctionHouseTestHelper.CreateRules(maximumRequestsPerSecond: 1)).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.SearchAsync(player, 1, SearchQuery).ConfigureAwait(false);
        await this._action.SearchAsync(player, 2, SearchQuery).ConfigureAwait(false);

        var view = player.View<IShowAuctionSearchResultPlugIn>();
        view.Verify(v => v.ShowAuctionSearchResultAsync(1, It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.Success)), Times.Once);
        view.Verify(v => v.ShowAuctionSearchResultAsync(2, It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.Busy && p.Listings.Count == 0)), Times.Once);
        Assert.That(market.Calls.Count(call => call.Method == nameof(IAuctionHouseMarket.SearchAsync)), Is.EqualTo(1));
    }

    /// <summary>
    /// The requests of a player reach the market one at a time.
    /// </summary>
    [Test]
    public async Task RequestsReachMarketOneAtATimeAsync()
    {
        var gate = new TaskCompletionSource();
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);
        market.Calls.Clear();
        market.Gate = gate.Task;

        var first = this._action.SearchAsync(player, 1, SearchQuery).AsTask();
        var second = this._action.SearchAsync(player, 2, SearchQuery).AsTask();
        await Task.Delay(100).ConfigureAwait(false);

        Assert.That(market.Calls, Has.Count.EqualTo(1));
        gate.SetResult();
        await Task.WhenAll(first, second).ConfigureAwait(false);
        Assert.That(market.Calls, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// After a goods purchase is refused because the quote changed or the stock is insufficient, a fresh quote
    /// with the request id of the purchase follows the answer.
    /// </summary>
    /// <param name="result">The result of the purchase.</param>
    [TestCase(AuctionHouseResult.QuoteChanged)]
    [TestCase(AuctionHouseResult.InsufficientStock)]
    public async Task FreshQuoteFollowsRefusedGoodsPurchaseAsync(AuctionHouseResult result)
    {
        var quotation = new AuctionGoodsQuotation(AuctionHouseResult.InsufficientStock, 7181, 0, 30, 20, 21_000_000, [new AuctionPriceTier(1_050_000, 20)]);
        var market = new FakeAuctionHouseMarket
        {
            BuyGoodsOutcome = new AuctionBuyGoodsOutcome(result, 7181, 0, 0, 0, 0, 0),
            Quotation = quotation,
        };
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);
        var answers = new List<string>();
        player.View<IShowAuctionBuyGoodsResultPlugIn>()
            .Setup(v => v.ShowAuctionBuyGoodsResultAsync(It.IsAny<ushort>(), It.IsAny<AuctionBuyGoodsOutcome>()))
            .Callback<ushort, AuctionBuyGoodsOutcome>((id, outcome) => answers.Add($"result {id} {outcome.Result}"))
            .Returns(ValueTask.CompletedTask);
        player.View<IShowAuctionGoodsQuotePlugIn>()
            .Setup(v => v.ShowAuctionGoodsQuoteAsync(It.IsAny<ushort>(), It.IsAny<AuctionGoodsQuotation>()))
            .Callback<ushort, AuctionGoodsQuotation>((id, q) => answers.Add($"quote {id} {q.Total}"))
            .Returns(ValueTask.CompletedTask);

        await this._action.BuyGoodsAsync(player, 4711, new AuctionBuyGoodsOrder(7181, 0, 30, 30_000_000)).ConfigureAwait(false);

        Assert.That(answers, Is.EqualTo(new[] { $"result 4711 {result}", "quote 4711 21000000" }));
        Assert.That(market.SingleArgument<AuctionGoodsQuoteQuery>(nameof(IAuctionHouseMarket.QuoteGoodsAsync)), Is.EqualTo(new AuctionGoodsQuoteQuery(7181, 0, 30)));
    }

    /// <summary>
    /// A successful goods purchase is not followed by a quote.
    /// </summary>
    [Test]
    public async Task SuccessfulGoodsPurchaseHasNoQuoteAsync()
    {
        var market = new FakeAuctionHouseMarket { BuyGoodsOutcome = new AuctionBuyGoodsOutcome(AuctionHouseResult.Success, 7181, 0, 18, 18_000_000, 18, 0) };
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.BuyGoodsAsync(player, 7, new AuctionBuyGoodsOrder(7181, 0, 18, 18_000_000)).ConfigureAwait(false);

        player.View<IShowAuctionGoodsQuotePlugIn>().Verify(v => v.ShowAuctionGoodsQuoteAsync(It.IsAny<ushort>(), It.IsAny<AuctionGoodsQuotation>()), Times.Never);
    }

    /// <summary>
    /// A listing whose expected posting fee differs from the current rules is answered with
    /// <see cref="AuctionHouseResult.RulesChanged"/>, followed by a fresh broker state, without reaching the market.
    /// </summary>
    [Test]
    public async Task ChangedPostingFeeIsAnsweredRulesChangedFollowedByStateAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var rules = AuctionHouseTestHelper.CreateRules();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market, rules).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);
        rules.PostingFeeFlat = 2_000;
        var answers = new List<string>();
        player.View<IShowAuctionCreateListingResultPlugIn>()
            .Setup(v => v.ShowAuctionCreateListingResultAsync(It.IsAny<ushort>(), It.IsAny<AuctionCreateListingOutcome>()))
            .Callback<ushort, AuctionCreateListingOutcome>((id, outcome) => answers.Add($"result {id} {outcome.Result}"))
            .Returns(ValueTask.CompletedTask);
        player.View<IShowAuctionHouseStatePlugIn>()
            .Setup(v => v.ShowAuctionHouseOpenedAsync(It.IsAny<AuctionBrokerState>()))
            .Callback<AuctionBrokerState>(state => answers.Add($"open {state.PostingFeeFlat}"))
            .Returns(ValueTask.CompletedTask);

        await this._action.CreateListingAsync(player, 8, new AuctionCreateListingOrder(12, 18, 1_000_000, 24, 181_000)).ConfigureAwait(false);

        Assert.That(answers, Is.EqualTo(new[] { "result 8 RulesChanged", "open 2000" }));
        Assert.That(market.Calls.Any(call => call.Method == nameof(IAuctionHouseMarket.CreateListingAsync)), Is.False);
    }

    /// <summary>
    /// A <see cref="AuctionHouseResult.RulesChanged"/> answer of the market is followed by a fresh broker state as well.
    /// </summary>
    [Test]
    public async Task RulesChangedOfMarketIsFollowedByStateAsync()
    {
        var market = new FakeAuctionHouseMarket { CreateListingOutcome = new AuctionCreateListingOutcome(AuctionHouseResult.RulesChanged, Guid.Empty, 0, 0) };
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.CreateListingAsync(player, 8, new AuctionCreateListingOrder(12, 18, 1_000_000, 24, 181_000)).ConfigureAwait(false);

        player.View<IShowAuctionCreateListingResultPlugIn>().Verify(v => v.ShowAuctionCreateListingResultAsync(8, It.Is<AuctionCreateListingOutcome>(o => o.Result == AuctionHouseResult.RulesChanged)), Times.Once);
        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseOpenedAsync(It.IsAny<AuctionBrokerState>()), Times.Exactly(2));
        Assert.That(market.SingleArgument<AuctionCreateListingOrder>(nameof(IAuctionHouseMarket.CreateListingAsync)), Is.EqualTo(new AuctionCreateListingOrder(12, 18, 1_000_000, 24, 181_000)));
    }

    /// <summary>
    /// A listing is checked against the rules before it reaches the market: quantity, duration and price range.
    /// </summary>
    /// <param name="quantity">The quantity.</param>
    /// <param name="unitPrice">The unit price.</param>
    /// <param name="durationHours">The duration in hours.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(0u, 1_000_000u, (byte)24, AuctionHouseResult.InvalidRequest)]
    [TestCase(18u, 1_000_000u, (byte)25, AuctionHouseResult.InvalidRequest)]
    [TestCase(18u, 0u, (byte)24, AuctionHouseResult.PriceOutOfRange)]
    [TestCase(30u, 100_000_000u, (byte)24, AuctionHouseResult.PriceOutOfRange)]
    public async Task ListingOutsideOfRulesIsRefusedAsync(uint quantity, uint unitPrice, byte durationHours, AuctionHouseResult expected)
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.CreateListingAsync(player, 8, new AuctionCreateListingOrder(12, quantity, unitPrice, durationHours, 181_000)).ConfigureAwait(false);

        player.View<IShowAuctionCreateListingResultPlugIn>().Verify(v => v.ShowAuctionCreateListingResultAsync(8, It.Is<AuctionCreateListingOutcome>(o => o.Result == expected)), Times.Once);
        Assert.That(market.Calls.Any(call => call.Method == nameof(IAuctionHouseMarket.CreateListingAsync)), Is.False);
    }

    /// <summary>
    /// A search with values outside of the protocol ranges is answered with <see cref="AuctionHouseResult.InvalidRequest"/>.
    /// </summary>
    [Test]
    public async Task SearchOutsideOfRangesIsInvalidAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.SearchAsync(player, 1, SearchQuery with { AncientSetMembers = [new AuctionAncientSetMember(4105, 3)] }).ConfigureAwait(false);
        await this._action.SearchAsync(player, 2, SearchQuery with { UpgradeMin = 9, UpgradeMax = 8 }).ConfigureAwait(false);
        await this._action.SearchAsync(player, 3, SearchQuery with { NameMatchTypes = Enumerable.Repeat((ushort)5, 49).ToList() }).ConfigureAwait(false);

        var view = player.View<IShowAuctionSearchResultPlugIn>();
        view.Verify(v => v.ShowAuctionSearchResultAsync(It.IsAny<ushort>(), It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.InvalidRequest)), Times.Exactly(3));
        Assert.That(market.Calls.Any(call => call.Method == nameof(IAuctionHouseMarket.SearchAsync)), Is.False);
    }

    /// <summary>
    /// An exception of the market is answered with <see cref="AuctionHouseResult.InternalError"/>.
    /// </summary>
    [Test]
    public async Task MarketExceptionIsAnsweredInternalErrorAsync()
    {
        var market = new FakeAuctionHouseMarket { Gate = Task.FromException(new InvalidOperationException("test")) };
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await this._action.CancelListingAsync(player, 9, new AuctionCancelListingOrder(Guid.NewGuid())).ConfigureAwait(false);

        player.View<IShowAuctionCancelListingResultPlugIn>().Verify(v => v.ShowAuctionCancelListingResultAsync(9, It.Is<AuctionCancelListingOutcome>(o => o.Result == AuctionHouseResult.InternalError)), Times.Once);
    }

    /// <summary>
    /// The close request of the client closes the broker; later requests are answered with <see cref="AuctionHouseResult.NotOpen"/>.
    /// </summary>
    [Test]
    public async Task CloseRequestClosesBrokerAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await new CloseNpcDialogAction().CloseNpcDialogAsync(player).ConfigureAwait(false);
        await this._action.SearchAsync(player, 5, SearchQuery).ConfigureAwait(false);

        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseClosedAsync(), Times.Once);
        player.View<IShowAuctionSearchResultPlugIn>().Verify(v => v.ShowAuctionSearchResultAsync(5, It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.NotOpen)), Times.Once);
        Assert.That(AuctionBrokerSession.IsBrokerOpen(player), Is.False);
    }

    /// <summary>
    /// A warp closes the broker; later requests are answered with <see cref="AuctionHouseResult.NotOpen"/>.
    /// </summary>
    [Test]
    public async Task WarpClosesBrokerAsync()
    {
        var market = new FakeAuctionHouseMarket();
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(market).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await player.WarpToAsync(CreateGate(player)).ConfigureAwait(false);
        await this._action.SearchAsync(player, 6, SearchQuery).ConfigureAwait(false);

        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.ChangingMap));
        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseClosedAsync(), Times.Once);
        player.View<IShowAuctionSearchResultPlugIn>().Verify(v => v.ShowAuctionSearchResultAsync(6, It.Is<AuctionSearchPage>(p => p.Result == AuctionHouseResult.NotOpen)), Times.Once);
    }

    /// <summary>
    /// A logout back to the character selection closes the broker.
    /// </summary>
    [Test]
    public async Task LogoutClosesBrokerAsync()
    {
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(new FakeAuctionHouseMarket()).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await new LogoutAction().LogoutAsync(player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);

        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseClosedAsync(), Times.Once);
        Assert.That(AuctionBrokerSession.IsBrokerOpen(player), Is.False);
    }

    /// <summary>
    /// A player with an open NPC dialog can't advance to the dead state, so the death doesn't end the dialog.
    /// This documents the behavior on which the closing on a deadly hit is based.
    /// </summary>
    [Test]
    public async Task DeathDoesNotLeaveNpcDialogAsync()
    {
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(new FakeAuctionHouseMarket()).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await player.KillInstantlyAsync().ConfigureAwait(false);

        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.NpcDialogOpened));
        Assert.That(player.IsAlive, Is.True);
    }

    /// <summary>
    /// A deadly hit closes the broker, although the NPC dialog stays open.
    /// </summary>
    [Test]
    public async Task DeadlyHitClosesBrokerAsync()
    {
        var player = await AuctionHouseTestHelper.CreatePlayerAsync(new FakeAuctionHouseMarket()).ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);
        var plugIn = new AuctionBrokerClosingPlugIn();

        player.Attributes![Stats.CurrentHealth] = 1;
        plugIn.AttackableGotHit(player, Mock.Of<IAttacker>(), default);
        Assert.That(AuctionBrokerSession.IsBrokerOpen(player), Is.True);

        player.Attributes[Stats.CurrentHealth] = 0;
        plugIn.AttackableGotHit(player, Mock.Of<IAttacker>(), default);
        for (var i = 0; i < 50 && AuctionBrokerSession.IsBrokerOpen(player); i++)
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        Assert.That(AuctionBrokerSession.IsBrokerOpen(player), Is.False);
        player.View<IShowAuctionHouseStatePlugIn>().Verify(v => v.ShowAuctionHouseClosedAsync(), Times.Once);
    }

    private static bool IsEqual(AuctionBrokerState actual, AuctionBrokerState expected)
        => actual with { DurationHours = NoDurations } == expected with { DurationHours = NoDurations }
           && actual.DurationHours.SequenceEqual(expected.DurationHours);

    private static ExitGate CreateGate(Player player)
    {
        return new ExitGate
        {
            Map = player.CurrentMap!.Definition,
            X1 = 100,
            X2 = 100,
            Y1 = 100,
            Y2 = 100,
            Direction = Direction.West,
        };
    }
}
