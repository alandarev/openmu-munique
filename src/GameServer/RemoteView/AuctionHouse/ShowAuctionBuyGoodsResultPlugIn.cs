// <copyright file="ShowAuctionBuyGoodsResultPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionBuyGoodsResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The packet has a fixed length. The game client has to support the <see cref="AuctionBuyGoodsResult"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionBuyGoodsResultPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionBuyGoodsResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("ED1FF09A-127A-4453-8298-44C2DBDACD5B")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionBuyGoodsResultPlugIn : IShowAuctionBuyGoodsResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionBuyGoodsResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionBuyGoodsResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionBuyGoodsResultAsync(ushort requestId, AuctionBuyGoodsOutcome outcome)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = AuctionBuyGoodsResultRef.Length;
            return ShowAuctionBuyGoodsResultPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, outcome);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionBuyGoodsResult"/> packet.
    /// </summary>
    /// <param name="target">The target, of <see cref="AuctionBuyGoodsResultRef.Length"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionBuyGoodsOutcome outcome)
    {
        target = target[..AuctionBuyGoodsResultRef.Length];
        target.Clear();
        _ = new AuctionBuyGoodsResultRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)outcome.Result,
            Type = outcome.ItemType,
            Level = outcome.Level,
            Quantity = outcome.Quantity,
            Paid = outcome.Paid,
            Delivered = outcome.Delivered,
            Kept = outcome.Kept,
        };
        return target.Length;
    }
}
