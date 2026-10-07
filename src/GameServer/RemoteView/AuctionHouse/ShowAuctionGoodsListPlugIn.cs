// <copyright file="ShowAuctionGoodsListPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionGoodsListPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The goods are ordered as the market returns them, by item type and level. The game client has to support the <see cref="AuctionGoodsList"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionGoodsListPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionGoodsListPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A7BB5BC3-A572-4974-8D9F-8C9061C0016D")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionGoodsListPlugIn : IShowAuctionGoodsListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionGoodsListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionGoodsListPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionGoodsListAsync(ushort requestId, AuctionGoodsPage page)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = AuctionGoodsListRef.GetRequiredSize(Math.Min(page.Goods.Count, AuctionHouseSerialization.MaximumRowCount));
            return ShowAuctionGoodsListPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, page);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionGoodsList"/> packet.
    /// </summary>
    /// <param name="target">The target, of at least <see cref="AuctionGoodsListRef.GetRequiredSize"/> for the goods.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="page">The page.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionGoodsPage page)
    {
        var goods = page.Goods.Take(AuctionHouseSerialization.MaximumRowCount).ToList();
        target = target[..AuctionGoodsListRef.GetRequiredSize(goods.Count)];
        target.Clear();
        var packet = new AuctionGoodsListRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)page.Result,
            Page = page.Page,
            TotalCount = page.TotalCount,
            Count = (byte)goods.Count,
        };

        for (var i = 0; i < goods.Count; i++)
        {
            var row = packet[i];
            row.Type = goods[i].ItemType;
            row.Level = goods[i].Level;
            row.LowestUnitPrice = goods[i].LowestUnitPrice;
            row.Available = goods[i].Available;
            row.ListingCount = goods[i].ListingCount;
        }

        return target.Length;
    }
}
