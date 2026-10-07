// <copyright file="ShowAuctionGoodsQuotePlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionGoodsQuotePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// At most eight price tiers are sent. The game client has to support the <see cref="AuctionGoodsQuote"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionGoodsQuotePlugIn_Name), Description = nameof(PlugInResources.ShowAuctionGoodsQuotePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1A3882A7-9921-4663-BDD1-3A0159998069")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionGoodsQuotePlugIn : IShowAuctionGoodsQuotePlugIn
{
    /// <summary>
    /// The maximum count of price tiers.
    /// </summary>
    internal const int MaximumTierCount = 8;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionGoodsQuotePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionGoodsQuotePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionGoodsQuoteAsync(ushort requestId, AuctionGoodsQuotation quotation)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = AuctionGoodsQuoteRef.GetRequiredSize(Math.Min(quotation.Tiers.Count, MaximumTierCount));
            return ShowAuctionGoodsQuotePlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, quotation);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the <see cref="AuctionGoodsQuote"/> packet.
    /// </summary>
    /// <param name="target">The target, of at least <see cref="AuctionGoodsQuoteRef.GetRequiredSize"/> for the tiers.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="quotation">The quotation.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionGoodsQuotation quotation)
    {
        var tiers = quotation.Tiers.Take(MaximumTierCount).ToList();
        target = target[..AuctionGoodsQuoteRef.GetRequiredSize(tiers.Count)];
        target.Clear();
        var packet = new AuctionGoodsQuoteRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)quotation.Result,
            Type = quotation.ItemType,
            Level = quotation.Level,
            RequestedQuantity = quotation.RequestedQuantity,
            FillableQuantity = quotation.FillableQuantity,
            Total = quotation.Total,
            TierCount = (byte)tiers.Count,
        };

        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = packet[i];
            tier.UnitPrice = tiers[i].UnitPrice;
            tier.Quantity = tiers[i].Quantity;
        }

        return target.Length;
    }
}
