// <copyright file="AuctionSellCheckRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionSellCheckRequest"/> of the auction house (0x7B, 0x5C identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionSellCheckRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionSellCheckRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E1A93D58-7B6C-4F20-A8D4-5C3B9E2F1A07")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionSellCheckRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionSellCheckRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionSellCheckRequestRef.Length)
        {
            return;
        }

        AuctionSellCheckRequest request = packet;

        await this._action.CheckSellAsync(player, request.RequestId, new AuctionSellCheckQuery(request.InventorySlot)).ConfigureAwait(false);
    }
}
