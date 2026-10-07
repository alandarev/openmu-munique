// <copyright file="AuctionHouseQuarantine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Threading;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Handles a player whose market transaction ended with an unknown outcome (<see cref="TransactionOutcomeUnknownException"/>):
/// the in-memory state of the player may contradict the database. The persistence context of the player already refuses
/// every further save; the auction house refuses the player's requests, and the player is disconnected, so that the
/// next login loads the database's state.
/// </summary>
internal static class AuctionHouseQuarantine
{
    /// <summary>
    /// Quarantines the player and starts its disconnection on a detached task, which runs after the current request
    /// released the player's locks.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="logger">The logger.</param>
    public static void Quarantine(Player player, ILogger logger)
    {
        if (!AuctionBrokerSession.Of(player).Quarantine())
        {
            return;
        }

        logger.LogError("The outcome of an auction house transaction of {player} is unknown; the player is disconnected without saving.", player);
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await player.DisconnectAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while disconnecting the quarantined player {player}.", player);
                }
            });
        }
    }
}
