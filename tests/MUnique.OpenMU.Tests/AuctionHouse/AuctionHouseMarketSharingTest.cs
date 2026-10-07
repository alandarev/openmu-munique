// <copyright file="AuctionHouseMarketSharingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.PlayerActions.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests that one <see cref="AuctionHouseMarket"/> serves all game contexts of a database and game configuration,
/// and that its index is loaded from the database.
/// </summary>
[TestFixture]
public class AuctionHouseMarketSharingTest
{
    /// <summary>
    /// Two game contexts of the same database and configuration get the same market; a listing created through one
    /// is bought through the other.
    /// </summary>
    [Test]
    public async Task TwoGameContextsShareOneMarketAsync()
    {
        var helper = new AuctionHouseMarketTestHelper();
        var firstContext = helper.CreateGameContext();
        var secondContext = helper.CreateGameContext();
        var market = AuctionHouseMarkets.Get(firstContext);
        Assert.That(AuctionHouseMarkets.Get(secondContext), Is.SameAs(market));
        Assert.That(AuctionHouseMarkets.Get(new AuctionHouseMarketTestHelper().CreateGameContext()), Is.Not.SameAs(market));

        var seller = await helper.CreatePlayerAsync(firstContext, "seller", 1_000_000).ConfigureAwait(false);
        await helper.AddItemAsync(seller, helper.Kris, AuctionHouseMarketTestHelper.FirstInventorySlot).ConfigureAwait(false);
        var created = await AuctionHouseMarkets.Get(firstContext).CreateListingAsync(seller, new AuctionCreateListingOrder(AuctionHouseMarketTestHelper.FirstInventorySlot, 1, 10_000, 48, 1_000)).ConfigureAwait(false);
        Assert.That(created.Result, Is.EqualTo(AuctionHouseResult.Success));

        var buyer = await helper.CreatePlayerAsync(secondContext, "buyer", 1_000_000).ConfigureAwait(false);
        var bought = await AuctionHouseMarkets.Get(secondContext).BuyListingAsync(buyer, new AuctionBuyListingOrder(created.ListingId, 10_000)).ConfigureAwait(false);
        Assert.That(bought.Result, Is.EqualTo(AuctionHouseResult.Success));
        Assert.That(market.Index.Get(created.ListingId), Is.Null);
    }

    /// <summary>
    /// The index is loaded with the active listings of the database at the first use; other listings are left out.
    /// </summary>
    [Test]
    public async Task IndexIsLoadedFromDatabaseAtFirstUseAsync()
    {
        var helper = new AuctionHouseMarketTestHelper();
        using var context = helper.ContextProvider.CreateNewContext();
        var active = CreateListing(context, AuctionListingState.Active);
        CreateListing(context, AuctionListingState.SoldOut);
        CreateListing(context, AuctionListingState.Cancelled);
        var market = AuctionHouseMarkets.Get(helper.CreateGameContext());

        await market.EnsureIndexLoadedAsync().ConfigureAwait(false);

        Assert.That(market.Index.Count, Is.EqualTo(1));
        Assert.That(market.Index.Get(active.GetId())?.RemainingQuantity, Is.EqualTo(3));
    }

    /// <summary>
    /// The action uses the shared market when the feature is active and has no explicit market.
    /// </summary>
    [Test]
    public async Task ActionUsesSharedMarketWithoutExplicitMarketAsync()
    {
        var helper = new AuctionHouseMarketTestHelper();
        var gameContext = helper.CreateGameContext();
        var player = await helper.CreatePlayerAsync(gameContext, "player").ConfigureAwait(false);
        await AuctionHouseTestHelper.OpenBrokerAsync(player).ConfigureAwait(false);

        await new AuctionHouseAction().CheckSellAsync(player, 3, new AuctionSellCheckQuery(AuctionHouseMarketTestHelper.FirstInventorySlot)).ConfigureAwait(false);

        player.View<IShowAuctionSellCheckResultPlugIn>().Verify(v => v.ShowAuctionSellCheckResultAsync(3, It.Is<AuctionSellCheckOutcome>(o => o.Result == AuctionHouseResult.Success && o.Reason == AuctionHouseSellCheckReason.NoItem)), Times.Once);
    }

    private static AuctionListing CreateListing(IContext context, AuctionListingState state)
    {
        var listing = context.CreateNew<AuctionListing>();
        listing.State = state;
        listing.Kind = AuctionHouseListingKind.Goods;
        listing.ItemGroup = 14;
        listing.ItemNumber = 13;
        listing.UnitPrice = 1_000;
        listing.ListedQuantity = 3;
        listing.RemainingQuantity = 3;
        listing.Escrow = context.CreateNew<ItemStorage>();
        return listing;
    }
}
