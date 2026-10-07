// <copyright file="AuctionOwnListingsRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.AuctionHouse;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for the <see cref="AuctionOwnListingsRequest"/> of the auction house (0x7B, 0x58 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionOwnListingsRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionOwnListingsRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7A4D1E63-2B9F-4C85-A3E0-6F8B1C2D5E97")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionOwnListingsRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionOwnListingsRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionOwnListingsRequestRef.Length)
        {
            return;
        }

        AuctionOwnListingsRequest request = packet;

        await this._action.GetOwnListingsAsync(player, request.RequestId).ConfigureAwait(false);
    }
}
