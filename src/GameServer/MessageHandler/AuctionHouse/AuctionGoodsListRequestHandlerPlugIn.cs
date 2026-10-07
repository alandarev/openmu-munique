// <copyright file="AuctionGoodsListRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionGoodsListRequest"/> of the auction house (0x7B, 0x52 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionGoodsListRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionGoodsListRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("84B0291D-7573-45DF-B237-5E2BF178A531")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionGoodsListRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionGoodsListRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < AuctionGoodsListRequestRef.GetRequiredSize(0))
        {
            return;
        }

        AuctionGoodsListRequest request = packet;
        if (packet.Length != AuctionGoodsListRequestRef.GetRequiredSize(request.TypeCount))
        {
            return;
        }

        var itemTypes = new List<ushort>(request.TypeCount);
        for (var i = 0; i < request.TypeCount; i++)
        {
            itemTypes.Add(request[i].Type);
        }

        await this._action.GetGoodsListAsync(player, request.RequestId, new AuctionGoodsListQuery(request.Page, itemTypes)).ConfigureAwait(false);
    }
}
