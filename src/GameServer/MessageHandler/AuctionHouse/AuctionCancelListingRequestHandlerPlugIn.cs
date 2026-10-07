// <copyright file="AuctionCancelListingRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionCancelListingRequest"/> of the auction house (0x7B, 0x57 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionCancelListingRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionCancelListingRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3E7C2A91-58D4-4F06-B1A7-9C5E2D7F4B10")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionCancelListingRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionCancelListingRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionCancelListingRequestRef.Length)
        {
            return;
        }

        AuctionCancelListingRequest request = packet;

        var order = new AuctionCancelListingOrder(new Guid(request.ListingId));
        await this._action.CancelListingAsync(player, request.RequestId, order).ConfigureAwait(false);
    }
}
