// <copyright file="ShowAuctionOwnListingsPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionOwnListingsPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// Each listing row has its own length, because of its item data; goods have none. The game client has to support the <see cref="AuctionOwnListings"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionOwnListingsPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionOwnListingsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("DAC765F6-88C9-42B4-8163-690A44AAE021")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionOwnListingsPlugIn : IShowAuctionOwnListingsPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionOwnListingsPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionOwnListingsPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionOwnListingsAsync(ushort requestId, AuctionOwnListingList listings)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var itemSerializer = this._player.ItemSerializer;
        int Write()
        {
            var size = GetMaximumSize(listings, itemSerializer);
            return ShowAuctionOwnListingsPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, listings, itemSerializer);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the maximum size of the <see cref="AuctionOwnListings"/> packet of the listings.
    /// </summary>
    /// <param name="listings">The listings.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The maximum size.</returns>
    internal static int GetMaximumSize(AuctionOwnListingList listings, IItemSerializer itemSerializer)
        => AuctionOwnListingsRef.GetRequiredSize(Math.Min(listings.Listings.Count, AuctionHouseSerialization.MaximumRowCount), AuctionOwnListingsRef.AuctionOwnListingRef.GetRequiredSize(itemSerializer.NeededSpace));

    /// <summary>
    /// Writes the <see cref="AuctionOwnListings"/> packet.
    /// </summary>
    /// <param name="target">The target, of at least <see cref="GetMaximumSize"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="listings">The listings.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionOwnListingList listings, IItemSerializer itemSerializer)
    {
        var rows = listings.Listings.Take(AuctionHouseSerialization.MaximumRowCount).ToList();
        target = target[..GetMaximumSize(listings, itemSerializer)];
        target.Clear();
        _ = new AuctionOwnListingsRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)listings.Result,
            Count = (byte)rows.Count,
        };

        var size = AuctionOwnListingsRef.GetRequiredSize(0, 0);
        var itemDataLengthOffset = AuctionOwnListingsRef.AuctionOwnListingRef.GetRequiredSize(0) - 1;
        foreach (var listing in rows)
        {
            var row = new AuctionOwnListingsRef.AuctionOwnListingRef(target[size..])
            {
                UnitPrice = listing.UnitPrice,
                Listed = listing.Listed,
                Remaining = listing.Remaining,
                SecondsLeft = listing.SecondsLeft,
                Kind = (AuctionListingKind)listing.Kind,
                Type = listing.ItemType,
                Level = listing.Level,
            };
            AuctionHouseSerialization.WriteId(row.ListingId, listing.ListingId);
            var itemDataLength = AuctionHouseSerialization.WriteItemData(target[(size + itemDataLengthOffset)..], listing.Item, itemSerializer);
            size += AuctionOwnListingsRef.AuctionOwnListingRef.GetRequiredSize(itemDataLength);
        }

        target[..size].SetPacketSize();
        return size;
    }
}
