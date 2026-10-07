// <copyright file="ShowAuctionNoticePlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionNoticePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The game client has to support the <see cref="AuctionNotice"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionNoticePlugIn_Name), Description = nameof(PlugInResources.ShowAuctionNoticePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("799FB976-0C34-4EF7-8F16-3E1CF92F2800")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionNoticePlugIn : IShowAuctionNoticePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionNoticePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionNoticePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionNoticeAsync(AuctionHouseNotice notice)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write() => ShowAuctionNoticePlugIn.Write(connection.Output.GetSpan(AuctionNoticeRef.Length), notice);

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionNotice"/> packet.
    /// </summary>
    /// <param name="target">The target, of <see cref="AuctionNoticeRef.Length"/>.</param>
    /// <param name="notice">The notice.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, AuctionHouseNotice notice)
    {
        target = target[..AuctionNoticeRef.Length];
        target.Clear();
        _ = new AuctionNoticeRef(target)
        {
            Kind = (AuctionNoticeKind)notice.Kind,
            Type = notice.ItemType,
            Level = notice.Level,
            Quantity = notice.Quantity,
            Net = notice.Net,
            PendingCollection = notice.PendingCollection,
        };
        return target.Length;
    }
}
