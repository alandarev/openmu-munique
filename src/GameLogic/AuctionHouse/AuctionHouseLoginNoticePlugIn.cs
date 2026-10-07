// <copyright file="AuctionHouseLoginNoticePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends the "deliveries waiting" notice to a character which enters the game while entries wait in its collection.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionHouseLoginNoticePlugIn_Name), Description = nameof(PlugInResources.AuctionHouseLoginNoticePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3E8A6F14-2C7B-4D59-A1E0-9F4B7D2C5E83")]
public class AuctionHouseLoginNoticePlugIn : IPlayerStateChangedPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection
            || currentState != PlayerState.EnteredWorld
            || player.SelectedCharacter is not { } character
            || AuctionHouseMarkets.GetMarket(player.GameContext) is null)
        {
            return;
        }

        var characterId = character.GetId();
        var pending = await player.RunPersistenceExclusiveAsync(() => player.PersistenceContext.GetAuctionCollectionEntryIdsAsync(characterId)).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return;
        }

        var notice = new AuctionHouseNotice(AuctionHouseNoticeKind.DeliveriesWaiting, 0, 0, 0, 0, (ushort)Math.Min(pending.Count, ushort.MaxValue));
        await player.InvokeViewPlugInAsync<IShowAuctionNoticePlugIn>(p => p.ShowAuctionNoticeAsync(notice)).ConfigureAwait(false);
    }
}
