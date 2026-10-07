// <copyright file="AuctionHouseMarkets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The markets of the process: one per persistence context provider (the database) and game configuration (by id),
/// so that all game servers of one configuration share a market, its locks and its index.
/// </summary>
public static class AuctionHouseMarkets
{
    private static readonly ConditionalWeakTable<IPersistenceContextProvider, ConcurrentDictionary<Guid, Lazy<AuctionHouseMarket>>> Markets = new();

    /// <summary>
    /// Gets the market behind the broker of a game context: none when the auction house feature isn't active, else the
    /// feature's <see cref="AuctionHouseFeaturePlugIn.Market"/> or the shared market of the game context
    /// (<see cref="Get"/>). A market of this type gets the game context registered for its deliveries.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The market, if the feature is active.</returns>
    public static IAuctionHouseMarket? GetMarket(IGameContext gameContext)
    {
        if (gameContext.FeaturePlugIns.GetPlugIn<AuctionHouseFeaturePlugIn>() is not { } feature)
        {
            return null;
        }

        if (feature.Market is { } market)
        {
            (market as AuctionHouseMarket)?.RegisterGameContext(gameContext);
            return market;
        }

        return Get(gameContext);
    }

    /// <summary>
    /// Gets the market of a game context, and creates it at the first request. The game context is registered for the
    /// deliveries of the market.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The market which is shared by all game contexts with the same database and game configuration.</returns>
    public static AuctionHouseMarket Get(IGameContext gameContext)
    {
        var markets = Markets.GetValue(gameContext.PersistenceContextProvider, _ => new ConcurrentDictionary<Guid, Lazy<AuctionHouseMarket>>());
        var market = markets.GetOrAdd(
                gameContext.Configuration.GetId(),
                _ => new Lazy<AuctionHouseMarket>(() => new AuctionHouseMarket(
                    gameContext.PersistenceContextProvider,
                    gameContext.Configuration,
                    gameContext.LoggerFactory.CreateLogger<AuctionHouseMarket>())))
            .Value;
        market.RegisterGameContext(gameContext);
        return market;
    }
}
