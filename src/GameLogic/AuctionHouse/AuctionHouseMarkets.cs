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
    /// Gets the market of a game context, and creates it at the first request.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The market which is shared by all game contexts with the same database and game configuration.</returns>
    public static AuctionHouseMarket Get(IGameContext gameContext)
    {
        var markets = Markets.GetValue(gameContext.PersistenceContextProvider, _ => new ConcurrentDictionary<Guid, Lazy<AuctionHouseMarket>>());
        return markets.GetOrAdd(
                gameContext.Configuration.GetId(),
                _ => new Lazy<AuctionHouseMarket>(() => new AuctionHouseMarket(
                    gameContext.PersistenceContextProvider,
                    gameContext.Configuration,
                    gameContext.LoggerFactory.CreateLogger<AuctionHouseMarket>())))
            .Value;
    }
}
