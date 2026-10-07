// <copyright file="AuctionGoodsQuoteRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionGoodsQuoteRequest"/> of the auction house (0x7B, 0x53 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionGoodsQuoteRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionGoodsQuoteRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D1047F00-F8C4-4633-922A-9235B82A234D")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionGoodsQuoteRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionGoodsQuoteRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionGoodsQuoteRequestRef.Length)
        {
            return;
        }

        AuctionGoodsQuoteRequest request = packet;

        await this._action.QuoteGoodsAsync(player, request.RequestId, new AuctionGoodsQuoteQuery(request.Type, request.Level, request.Quantity)).ConfigureAwait(false);
    }
}
