// <copyright file="AuctionCreateListingRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionCreateListingRequest"/> of the auction house (0x7B, 0x56 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionCreateListingRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionCreateListingRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("26FDF2DD-40A7-4EBA-9CC2-13624F37F4AE")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionCreateListingRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionCreateListingRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionCreateListingRequestRef.Length)
        {
            return;
        }

        AuctionCreateListingRequest request = packet;

        var order = new AuctionCreateListingOrder(request.InventorySlot, request.Quantity, request.UnitPrice, request.DurationHours, request.ExpectedPostingFee);
        await this._action.CreateListingAsync(player, request.RequestId, order).ConfigureAwait(false);
    }
}
