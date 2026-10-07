// <copyright file="AuctionListingIndex.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence;

/// <summary>
/// The in-memory read model of the active listings of a market. It is updated only after a change was committed, so it
/// can lag behind the database for a moment; every write re-validates against the locked rows.
/// </summary>
/// <remarks>
/// Publication is monotonic, because the snapshots of several committed transactions can arrive in any order: the
/// remaining quantity of a listing only decreases, and a listing which left the active state stays removed (its id is
/// kept as a tombstone until the next <see cref="Reset"/>). An older snapshot therefore can't resurrect a sold, cancelled
/// or expired listing, nor raise its remaining quantity.
/// </remarks>
public sealed class AuctionListingIndex
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, AuctionIndexedListing> _listings = [];
    private readonly HashSet<Guid> _removedListings = [];

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
            this._removedListings.Clear();
            foreach (var listing in listings)
            {
                this._listings[listing.Id] = listing;
            }
        }
    }

    /// <summary>
    /// Applies a committed state of a listing, monotonically: an active listing is added, or replaced when it holds
    /// fewer remaining units than the indexed state; any other state removes the listing for good. Older states are
    /// ignored.
    /// </summary>
    /// <param name="listing">The listing, as committed.</param>
    public void Apply(AuctionListing listing)
    {
        var id = listing.GetId();
        var isActive = listing.State == AuctionListingState.Active && listing.RemainingQuantity > 0;
        var indexed = isActive ? AuctionIndexedListing.Of(listing) : null;
        lock (this._syncRoot)
        {
            if (this._removedListings.Contains(id))
            {
                return;
            }

            if (indexed is null)
            {
                this._listings.Remove(id);
                this._removedListings.Add(id);
                return;
            }

            if (!this._listings.TryGetValue(id, out var current) || indexed.RemainingQuantity < current.RemainingQuantity)
            {
                this._listings[id] = indexed;
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
    /// Gets the count of active listings of a seller whose duration didn't end yet.
    /// </summary>
    /// <param name="sellerCharacterId">The identifier of the selling character.</param>
    /// <param name="now">The current point in time (UTC); listings which expired at that time aren't counted.</param>
    /// <returns>The count of active listings.</returns>
    public int CountOfSeller(Guid sellerCharacterId, DateTime now)
    {
        lock (this._syncRoot)
        {
            return this._listings.Values.Count(listing => listing.SellerCharacterId == sellerCharacterId && listing.ExpiresAt > now);
        }
    }

    /// <summary>
    /// Gets the active listings which match a predicate.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The matching listings, in no particular order.</returns>
    public List<AuctionIndexedListing> Where(Func<AuctionIndexedListing, bool> predicate)
    {
        lock (this._syncRoot)
        {
            return this._listings.Values.Where(predicate).ToList();
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
    /// <param name="now">The current point in time (UTC); listings which expired at that time are left out.</param>
    /// <returns>The listings in fill order.</returns>
    public IReadOnlyList<AuctionIndexedListing> GetGoodsInFillOrder(byte group, short number, byte level, Guid excludedSellerCharacterId, DateTime now)
    {
        List<AuctionIndexedListing> result;
        lock (this._syncRoot)
        {
            result = this._listings.Values
                .Where(listing => listing.Kind == AuctionHouseListingKind.Goods
                                  && listing.ItemGroup == group
                                  && listing.ItemNumber == number
                                  && listing.ItemLevel == level
                                  && listing.SellerCharacterId != excludedSellerCharacterId
                                  && listing.ExpiresAt > now)
                .ToList();
        }

        result.Sort(AuctionGoodsFill.CompareFillOrder);
        return result;
    }
}
