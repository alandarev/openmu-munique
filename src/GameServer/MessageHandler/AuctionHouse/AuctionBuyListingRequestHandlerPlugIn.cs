// <copyright file="AuctionBuyListingRequestHandlerPlugIn.cs" company="MUnique">
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
/// Packet handler for the <see cref="AuctionBuyListingRequest"/> of the auction house (0x7B, 0x54 identifier).
/// </summary>
/// <remarks>
/// A packet whose length doesn't match its layout is ignored without an answer.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionBuyListingRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.AuctionBuyListingRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("AEA55B21-EFBF-4D48-8830-820FD9788D79")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CustomGroupHandlerPlugIn.GroupKey)]
internal class AuctionBuyListingRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly AuctionHouseAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AuctionBuyListingRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != AuctionBuyListingRequestRef.Length)
        {
            return;
        }

        AuctionBuyListingRequest request = packet;

        var order = new AuctionBuyListingOrder(new Guid(request.ListingId), request.ReviewedPrice);
        await this._action.BuyListingAsync(player, request.RequestId, order).ConfigureAwait(false);
    }
}
