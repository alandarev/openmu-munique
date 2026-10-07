// <copyright file="AuctionHouseSearchSortTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Tests for the sort orders of the auction house search, their tie-breakers (listing time ascending, then id) and
/// stable paging.
/// </summary>
[TestFixture]
public class AuctionHouseSearchSortTest
{
    private const byte Slot = AuctionHouseMarketTestHelper.FirstInventorySlot;

    private static readonly AuctionSearchQuery AnyItem = new(0, AuctionHouseSortOrder.PriceAscending, AuctionHouseEquipmentSlots.None, 0, AuctionHouseClassLine.Any, AuctionHouseSearchOptions.None, AuctionHouseItemFamily.Any, 0, 15, 0, 0, 0, 0, [], []);

    private AuctionHouseMarketTestHelper _helper = null!;
    private ManualTimeProvider _time = null!;
    private AuctionHouseMarket _market = null!;
    private Player _seller = null!;
    private Player _buyer = null!;
    private Dictionary<Guid, string> _names = null!;

    /// <summary>
    /// Creates a market with a controllable time.
    /// </summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._helper = new AuctionHouseMarketTestHelper();
        this._time = new ManualTimeProvider();
        var gameContext = this._helper.CreateGameContext();
        this._market = new AuctionHouseMarket(this._helper.ContextProvider, this._helper.Configuration, new NullLogger<AuctionHouseMarket>(), this._time);
        this._seller = await this._helper.CreatePlayerAsync(gameContext, "seller", 100_000_000).ConfigureAwait(false);
        this._buyer = await this._helper.CreatePlayerAsync(gameContext, "buyer").ConfigureAwait(false);
        this._names = [];
    }

    /// <summary>
    /// Each order, with ties broken by listing time ascending.
    /// </summary>
    /// <param name="sort">The sort order.</param>
    /// <param name="expected">The expected names, in order.</param>
    [TestCase(AuctionHouseSortOrder.PriceAscending, "b,d,a,c")]
    [TestCase(AuctionHouseSortOrder.PriceDescending, "c,a,b,d")]
    [TestCase(AuctionHouseSortOrder.EndingSoon, "a,d,b,c")]
    [TestCase(AuctionHouseSortOrder.Newest, "c,d,b,a")]
    public async Task SearchSortsByOrderAsync(AuctionHouseSortOrder sort, string expected)
    {
        // "a", "b", "c" are created one minute apart; "d" half a minute after "b", with a shorter duration and the same
        // price as "b", so that the equal prices are ordered by listing time.
        await this.ListAsync("a", 3_000, 48).ConfigureAwait(false);
        await this.ListAsync("b", 1_000, 72).ConfigureAwait(false);
        await this.ListAsync("c", 9_000, 72).ConfigureAwait(false);
        this._time.Advance(TimeSpan.FromSeconds(-90));
        await this.ListAsync("d", 1_000, 48, keepTime: true).ConfigureAwait(false);

        var page = await this._market.SearchAsync(this._buyer, AnyItem with { Sort = sort }).ConfigureAwait(false);

        Assert.That(page.Listings.Select(row => this._names[row.ListingId]), Is.EqualTo(expected.Split(',')));
    }

    /// <summary>
    /// Pages over listings with equal sort keys are stable: every listing appears exactly once, in creation order.
    /// </summary>
    /// <param name="sort">The sort order.</param>
    [TestCase(AuctionHouseSortOrder.PriceAscending)]
    [TestCase(AuctionHouseSortOrder.PriceDescending)]
    [TestCase(AuctionHouseSortOrder.EndingSoon)]
    [TestCase(AuctionHouseSortOrder.Newest)]
    public async Task PagesWithEqualKeysAreStableAsync(AuctionHouseSortOrder sort)
    {
        // Equal price, listing time and expiry for all: only the id decides.
        for (var i = 0; i < 7; i++)
        {
            await this.ListAsync("x" + i, 5_000, 48, keepTime: true).ConfigureAwait(false);
        }

        this._helper.Rules.PageSize = 3;
        var seen = new List<Guid>();
        for (ushort pageNumber = 0; pageNumber < 4; pageNumber++)
        {
            var first = await this._market.SearchAsync(this._buyer, AnyItem with { Sort = sort, Page = pageNumber }).ConfigureAwait(false);
            var again = await this._market.SearchAsync(this._buyer, AnyItem with { Sort = sort, Page = pageNumber }).ConfigureAwait(false);
            Assert.That(again.Listings.Select(row => row.ListingId), Is.EqualTo(first.Listings.Select(row => row.ListingId)));
            Assert.That(first.TotalCount, Is.EqualTo(7));
            seen.AddRange(first.Listings.Select(row => row.ListingId));
        }

        Assert.That(seen, Is.EqualTo(seen.Order()), "equal keys are ordered by id");
        Assert.That(seen, Is.Unique);
        Assert.That(seen, Has.Count.EqualTo(7));
    }

    private async Task ListAsync(string name, uint price, byte durationHours, bool keepTime = false)
    {
        await this._helper.AddItemAsync(this._seller, this._helper.Kris, Slot).ConfigureAwait(false);
        var outcome = await this._market.CreateListingAsync(this._seller, new AuctionCreateListingOrder(Slot, 1, price, durationHours, 0)).ConfigureAwait(false);
        Assert.That(outcome.Result, Is.EqualTo(AuctionHouseResult.Success), name);
        this._names[outcome.ListingId] = name;
        if (!keepTime)
        {
            this._time.Advance(TimeSpan.FromMinutes(1));
        }
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
