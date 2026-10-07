// <copyright file="AuctionClaimRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.PlayerActions.AuctionHouse;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for the <see cref="AuctionClaimRequest"/> of the auction house (0x7B, 0x5A identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionClaimRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionClaimRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C5E81A37-4F2D-4B9C-9E63-2D7A0F1B8C54")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionClaimRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionClaimRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionClaimRequestRef.Length)
        {
            return;
        }

        AuctionClaimRequest request = packet;

        var entryId = new Guid(request.EntryId);
        var order = new AuctionClaimOrder(entryId == Guid.Empty ? null : entryId);
        await this._action.ClaimAsync(player, request.RequestId, order).ConfigureAwait(false);
    }
}
