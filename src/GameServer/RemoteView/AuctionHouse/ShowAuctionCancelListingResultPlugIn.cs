// <copyright file="ShowAuctionCancelListingResultPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionCancelListingResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The packet has a fixed length. The game client has to support the <see cref="AuctionCancelListingResult"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionCancelListingResultPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionCancelListingResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2E5C9CB4-A8F4-4947-B63B-2D8CF89B7680")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionCancelListingResultPlugIn : IShowAuctionCancelListingResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionCancelListingResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionCancelListingResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionCancelListingResultAsync(ushort requestId, AuctionCancelListingOutcome outcome)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = AuctionCancelListingResultRef.Length;
            return ShowAuctionCancelListingResultPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, outcome);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionCancelListingResult"/> packet.
    /// </summary>
    /// <param name="target">The target, of <see cref="AuctionCancelListingResultRef.Length"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionCancelListingOutcome outcome)
    {
        target = target[..AuctionCancelListingResultRef.Length];
        target.Clear();
        var packet = new AuctionCancelListingResultRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)outcome.Result,
            ToInventory = outcome.ToInventory,
            ToCollection = outcome.ToCollection,
            RefundedFee = outcome.RefundedFee,
        };
        AuctionHouseSerialization.WriteId(packet.ListingId, outcome.ListingId);
        return target.Length;
    }
}
