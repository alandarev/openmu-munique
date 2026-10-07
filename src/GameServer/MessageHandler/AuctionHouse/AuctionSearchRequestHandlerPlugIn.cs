// <copyright file="AuctionSearchRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionSearchRequest"/> of the auction house (0x7B, 0x51 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionSearchRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionSearchRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9C9F5C3C-50F7-4578-A501-2D044B3D77F1")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionSearchRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionSearchRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < AuctionSearchRequestRef.GetRequiredSize(0))
        {
            return;
        }

        AuctionSearchRequest request = packet;
        if (packet.Length != AuctionSearchRequestRef.GetRequiredSize(request.EntryCount))
        {
            return;
        }

        var nameMatchTypes = new List<ushort>();
        var ancientSetMembers = new List<AuctionAncientSetMember>();
        for (var i = 0; i < request.EntryCount; i++)
        {
            var entry = request[i];
            if (entry.Discriminator == 0)
            {
                nameMatchTypes.Add(entry.Type);
            }
            else
            {
                ancientSetMembers.Add(new AuctionAncientSetMember(entry.Type, entry.Discriminator));
            }
        }

        var query = new AuctionSearchQuery(
            request.Page,
            (AuctionHouseSortOrder)request.Sort,
            (AuctionHouseEquipmentSlots)request.SlotMask,
            request.GroupMask,
            (AuctionHouseClassLine)request.ClassLine,
            (AuctionHouseSearchOptions)request.Flags,
            (AuctionHouseItemFamily)request.Family,
            request.UpgradeMin,
            request.UpgradeMax,
            request.ExcellentMask,
            request.MinExcellentCount,
            request.MinSockets,
            request.MaxPrice,
            nameMatchTypes,
            ancientSetMembers);
        await this._action.SearchAsync(player, request.RequestId, query).ConfigureAwait(false);
    }
}
