// <copyright file="ShowAuctionCollectionPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionCollectionPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// Each entry has its own length, because of its item data; only individual items have one. The game client has to support the <see cref="AuctionCollection"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionCollectionPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionCollectionPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5CEFCAE1-0B06-4F69-B031-4D72AC40B0FD")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionCollectionPlugIn : IShowAuctionCollectionPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionCollectionPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionCollectionPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionCollectionAsync(ushort requestId, AuctionCollectionPage page)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var itemSerializer = this._player.ItemSerializer;
        int Write()
        {
            var size = GetMaximumSize(page, itemSerializer);
            return ShowAuctionCollectionPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, page, itemSerializer);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the maximum size of the <see cref="AuctionCollection"/> packet of a page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The maximum size.</returns>
    internal static int GetMaximumSize(AuctionCollectionPage page, IItemSerializer itemSerializer)
        => AuctionCollectionRef.GetRequiredSize(Math.Min(page.Entries.Count, AuctionHouseSerialization.MaximumRowCount), AuctionCollectionRef.AuctionCollectionEntryRef.GetRequiredSize(itemSerializer.NeededSpace));

    /// <summary>
    /// Writes the <see cref="AuctionCollection"/> packet.
    /// </summary>
    /// <param name="target">The target, of at least <see cref="GetMaximumSize"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="page">The page.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionCollectionPage page, IItemSerializer itemSerializer)
    {
        var entries = page.Entries.Take(AuctionHouseSerialization.MaximumRowCount).ToList();
        target = target[..GetMaximumSize(page, itemSerializer)];
        target.Clear();
        _ = new AuctionCollectionRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)page.Result,
            Page = page.Page,
            TotalCount = page.TotalCount,
            Count = (byte)entries.Count,
        };

        var size = AuctionCollectionRef.GetRequiredSize(0, 0);
        var itemDataLengthOffset = AuctionCollectionRef.AuctionCollectionEntryRef.GetRequiredSize(0) - 1;
        foreach (var entry in entries)
        {
            var row = new AuctionCollectionRef.AuctionCollectionEntryRef(target[size..])
            {
                Kind = (AuctionCollectionEntryKind)entry.Kind,
                Type = entry.ItemType,
                Level = entry.Level,
                Quantity = entry.Quantity,
                Gross = entry.Gross,
                Fee = entry.Fee,
                Net = entry.Net,
            };
            AuctionHouseSerialization.WriteId(row.EntryId, entry.EntryId);
            var itemDataLength = AuctionHouseSerialization.WriteItemData(target[(size + itemDataLengthOffset)..], entry.Item, itemSerializer);
            size += AuctionCollectionRef.AuctionCollectionEntryRef.GetRequiredSize(itemDataLength);
        }

        target[..size].SetPacketSize();
        return size;
    }
}
