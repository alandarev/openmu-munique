// <copyright file="PlayerPersistenceQuarantine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Threading;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Quarantines a player whose in-memory state may contradict the database, e.g. after a
/// <see cref="TransactionOutcomeUnknownException"/>: nothing of the player may move anywhere anymore until the player is
/// disconnected, so that the next login loads the database's state.
/// </summary>
/// <remarks>
/// The persistence context of the player refuses every save and transaction (<see cref="IPlayerContext.IsQuarantined"/>);
/// the packet funnel of the game server drops every further packet of the player; actions of other players which take
/// items or Zen from the player (personal store purchase, trade) are refused; the personal store is closed here.
/// </remarks>
public static class PlayerPersistenceQuarantine
{
    /// <summary>
    /// Quarantines the player: its persistence context, its personal store, and starts its disconnection on a detached
    /// task, which runs after the current request released the player's locks.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public static async ValueTask QuarantineAsync(Player player)
    {
        player.PersistenceContext.Quarantine();
        player.Logger.LogError("The persistence of {player} is quarantined; the player is disconnected without saving.", player);
        try
        {
            await new CloseStoreAction().CloseStoreAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Error while closing the personal store of the quarantined player {player}.", player);
        }

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
                    player.Logger.LogError(ex, "Error while disconnecting the quarantined player {player}.", player);
                }
            });
        }
    }
}
