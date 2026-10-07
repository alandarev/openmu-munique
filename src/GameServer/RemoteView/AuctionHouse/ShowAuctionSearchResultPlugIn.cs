// <copyright file="ShowAuctionSearchResultPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowAuctionSearchResultPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// Each listing row has its own length, because of its item data. The game client has to support the <see cref="AuctionSearchResult"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionSearchResultPlugIn_Name), Description = nameof(PlugInResources.ShowAuctionSearchResultPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("05415679-20A7-4682-9F0B-A10C470489B7")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionSearchResultPlugIn : IShowAuctionSearchResultPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionSearchResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionSearchResultPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowAuctionSearchResultAsync(ushort requestId, AuctionSearchPage page)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var itemSerializer = this._player.ItemSerializer;
        int Write()
        {
            var size = GetMaximumSize(page, itemSerializer);
            return ShowAuctionSearchResultPlugIn.Write(connection.Output.GetSpan(size)[..size], requestId, page, itemSerializer);
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the maximum size of the <see cref="AuctionSearchResult"/> packet of a page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The maximum size.</returns>
    internal static int GetMaximumSize(AuctionSearchPage page, IItemSerializer itemSerializer)
        => AuctionSearchResultRef.GetRequiredSize(Math.Min(page.Listings.Count, AuctionHouseSerialization.MaximumRowCount), AuctionSearchResultRef.AuctionSearchListingRef.GetRequiredSize(itemSerializer.NeededSpace));

    /// <summary>
    /// Writes the <see cref="AuctionSearchResult"/> packet.
    /// </summary>
    /// <param name="target">The target, of at least <see cref="GetMaximumSize"/>.</param>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="page">The page.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, ushort requestId, AuctionSearchPage page, IItemSerializer itemSerializer)
    {
        var listings = page.Listings.Take(AuctionHouseSerialization.MaximumRowCount).ToList();
        target = target[..GetMaximumSize(page, itemSerializer)];
        target.Clear();
        _ = new AuctionSearchResultRef(target)
        {
            RequestId = requestId,
            Result = (AuctionResult)page.Result,
            Page = page.Page,
            TotalCount = page.TotalCount,
            Count = (byte)listings.Count,
        };

        var size = AuctionSearchResultRef.GetRequiredSize(0, 0);
        var itemDataLengthOffset = AuctionSearchResultRef.AuctionSearchListingRef.GetRequiredSize(0) - 1;
        foreach (var listing in listings)
        {
            var row = new AuctionSearchResultRef.AuctionSearchListingRef(target[size..])
            {
                Price = listing.Price,
                SecondsLeft = listing.SecondsLeft,
                Seller = AuctionHouseSerialization.CutName(listing.SellerName),
                Flags = (byte)((listing.RequirementsMet ? 0x01 : 0x00) | (listing.IsOwnListing ? 0x02 : 0x00)),
            };
            AuctionHouseSerialization.WriteId(row.ListingId, listing.ListingId);
            var itemDataLength = AuctionHouseSerialization.WriteItemData(target[(size + itemDataLengthOffset)..], listing.Item, itemSerializer);
            size += AuctionSearchResultRef.AuctionSearchListingRef.GetRequiredSize(itemDataLength);
        }

        target[..size].SetPacketSize();
        return size;
    }
}
