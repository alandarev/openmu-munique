// <copyright file="AuctionBrokerClosingPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.AuctionHouse;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Closes the auction house broker of a player when its dialog ends, and sends the broker state "closed".
/// </summary>
/// <remarks>
/// The dialog ends when the player state leaves <see cref="PlayerState.NpcDialogOpened"/>: the close request of
/// the game client, a warp, a logout or a disconnect. A player with an open NPC dialog can't advance to
/// <see cref="PlayerState.Dead"/>, so a deadly hit doesn't end the dialog; it closes the broker nevertheless.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionBrokerClosingPlugIn_Name), Description = nameof(PlugInResources.AuctionBrokerClosingPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5D0E8A57-6B14-4B7D-A2D1-3F1E5C8B9A64")]
public class AuctionBrokerClosingPlugIn : IPlayerStateChangedPlugIn, IAttackableGotHitPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.NpcDialogOpened || currentState == PlayerState.NpcDialogOpened)
        {
            return;
        }

        await this._action.CloseBrokerAsync(player, !currentState.IsDisconnectedOrFinished()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        if (attackable is not Player player
            || player.Attributes is not { } attributes
            || attributes[Stats.CurrentHealth] >= 1
            || !AuctionBrokerSession.IsBrokerOpen(player))
        {
            return;
        }

        _ = this.CloseAfterDeadlyHitAsync(player);
    }

    private async Task CloseAfterDeadlyHitAsync(Player player)
    {
        try
        {
            await this._action.CloseBrokerAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't close the auction house broker of player {player} after a deadly hit", player);
        }
    }
}
