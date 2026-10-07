// <copyright file="AuctionBuyGoodsRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionBuyGoodsRequest"/> of the auction house (0x7B, 0x55 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionBuyGoodsRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionBuyGoodsRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("19E1476A-DA9C-40EE-83D8-EA7469F77016")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionBuyGoodsRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionBuyGoodsRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionBuyGoodsRequestRef.Length)
        {
            return;
        }

        AuctionBuyGoodsRequest request = packet;

        var order = new AuctionBuyGoodsOrder(request.Type, request.Level, request.Quantity, request.ReviewedTotal);
        await this._action.BuyGoodsAsync(player, request.RequestId, order).ConfigureAwait(false);
    }
}
