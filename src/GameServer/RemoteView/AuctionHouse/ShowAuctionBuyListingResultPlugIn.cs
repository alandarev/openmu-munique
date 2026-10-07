// <copyright file="ShowAuctionBuyListingResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowAuctionBuyListingResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The packet has a fixed length. The game client has to support the <see cref="AuctionBuyListingResult"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionBuyListingResultPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionBuyListingResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("BE0F1133-2CCF-428A-8B39-7F109A953619")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionBuyListingResultPlugIn : IShowAuctionBuyListingResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionBuyListingResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionBuyListingResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionBuyListingResultAsync(ushort requestId, AuctionBuyListingOutcome outcome)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = AuctionBuyListingResultRef.Length;
            return ShowAuctionBuyListingResultPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, outcome);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionBuyListingResult"/> packet.
    /// </summary>
    /// <param name="target">The target, of <see cref="AuctionBuyListingResultRef.Length"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionBuyListingOutcome outcome)
    {
        target = target[..AuctionBuyListingResultRef.Length];
        target.Clear();
        var packet = new AuctionBuyListingResultRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)outcome.Result,
            Paid = outcome.Paid,
            Delivery = (AuctionDelivery)outcome.Delivery,
        };
        AuctionHouseSerialization.WriteId(packet.ListingId, outcome.ListingId);
        return target.Length;
    }
}
