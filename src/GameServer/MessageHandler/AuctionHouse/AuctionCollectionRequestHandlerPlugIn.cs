// <copyright file="AuctionCollectionRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionCollectionRequest"/> of the auction house (0x7B, 0x59 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionCollectionRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionCollectionRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9B2F6C14-E3A8-4D71-8F50-1A7C3E9D2B46")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionCollectionRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionCollectionRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionCollectionRequestRef.Length)
        {
            return;
        }

        AuctionCollectionRequest request = packet;

        await this._action.GetCollectionAsync(player, request.RequestId, new AuctionCollectionQuery(request.Page)).ConfigureAwait(false);
    }
}
