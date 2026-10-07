// <copyright file="ShowAuctionSellCheckResultPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionSellCheckResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The packet has a fixed length. The game client has to support the <see cref="AuctionSellCheckResult"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionSellCheckResultPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionSellCheckResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B7D15F94-EB8F-4F64-81B5-FAEB80524A1A")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionSellCheckResultPlugIn : IShowAuctionSellCheckResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionSellCheckResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionSellCheckResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionSellCheckResultAsync(ushort requestId, AuctionSellCheckOutcome outcome)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = AuctionSellCheckResultRef.Length;
            return ShowAuctionSellCheckResultPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, outcome);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionSellCheckResult"/> packet.
    /// </summary>
    /// <param name="target">The target, of <see cref="AuctionSellCheckResultRef.Length"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionSellCheckOutcome outcome)
    {
        target = target[..AuctionSellCheckResultRef.Length];
        target.Clear();
        _ = new AuctionSellCheckResultRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)outcome.Result,
            InventorySlot = outcome.InventorySlot,
            Reason = (AuctionSellCheckReason)outcome.Reason,
            Kind = (AuctionListingKind)outcome.Kind,
            Type = outcome.ItemType,
            Level = outcome.Level,
            Available = outcome.Available,
            LowestUnitPrice = outcome.LowestUnitPrice,
            ActiveListings = outcome.ActiveListings,
        };
        return target.Length;
    }
}
