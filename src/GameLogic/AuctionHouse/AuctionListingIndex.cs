// <copyright file="AuctionListingIndex.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence;

/// <summary>
/// The in-memory read model of the active listings of a market. It is updated only after a change was committed, so it
/// can lag behind the database for a moment; every write re-validates against the locked rows.
/// </summary>
public sealed class AuctionListingIndex
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, AuctionIndexedListing> _listings = [];

    /// <summary>
    /// Gets the count of active listings.
    /// </summary>
    public int Count
    {
        get
        {
            lock (this._syncRoot)
            {
                return this._listings.Count;
            }
        }
    }

    /// <summary>
    /// Replaces the content of the index.
    /// </summary>
    /// <param name="listings">The active listings.</param>
    public void Reset(IEnumerable<AuctionIndexedListing> listings)
    {
        lock (this._syncRoot)
        {
            this._listings.Clear();
            foreach (var listing in listings)
            {
                this._listings[listing.Id] = listing;
            }
        }
    }

    /// <summary>
    /// Applies the committed state of a listing: an active listing is added or replaced, any other is removed.
    /// </summary>
    /// <param name="listing">The listing, as committed.</param>
    public void Apply(AuctionListing listing)
    {
        var id = listing.GetId();
        if (listing.State == AuctionListingState.Active && listing.RemainingQuantity > 0)
        {
            var indexed = AuctionIndexedListing.Of(listing);
            lock (this._syncRoot)
            {
                this._listings[id] = indexed;
            }
        }
        else
        {
            lock (this._syncRoot)
            {
                this._listings.Remove(id);
            }
        }
    }

    /// <summary>
    /// Gets an active listing.
    /// </summary>
    /// <param name="id">The listing id.</param>
    /// <returns>The listing, if it is active.</returns>
    public AuctionIndexedListing? Get(Guid id)
    {
        lock (this._syncRoot)
        {
            return this._listings.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Gets the count of active listings of a seller.
    /// </summary>
    /// <param name="sellerCharacterId">The identifier of the selling character.</param>
    /// <returns>The count of active listings.</returns>
    public int CountOfSeller(Guid sellerCharacterId)
    {
        lock (this._syncRoot)
        {
            return this._listings.Values.Count(listing => listing.SellerCharacterId == sellerCharacterId);
        }
    }

    /// <summary>
    /// Gets the active goods listings of an item type and level, without those of the excluded seller, in the order in
    /// which a purchase fills them: cheapest unit price first, then oldest (creation time, then id).
    /// </summary>
    /// <param name="group">The group of the item definition.</param>
    /// <param name="number">The number of the item definition.</param>
    /// <param name="level">The item level.</param>
    /// <param name="excludedSellerCharacterId">The identifier of the character whose listings are left out.</param>
    /// <returns>The listings in fill order.</returns>
    public IReadOnlyList<AuctionIndexedListing> GetGoodsInFillOrder(byte group, short number, byte level, Guid excludedSellerCharacterId)
    {
        List<AuctionIndexedListing> result;
        lock (this._syncRoot)
        {
            result = this._listings.Values
                .Where(listing => listing.Kind == AuctionHouseListingKind.Goods
                                  && listing.ItemGroup == group
                                  && listing.ItemNumber == number
                                  && listing.ItemLevel == level
                                  && listing.SellerCharacterId != excludedSellerCharacterId)
                .ToList();
        }

        result.Sort(AuctionGoodsFill.CompareFillOrder);
        return result;
    }
}
