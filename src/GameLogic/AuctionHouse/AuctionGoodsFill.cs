// <copyright file="AuctionGoodsFill.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// How a quantity of goods is filled from listings: cheapest unit price first, then the oldest listing (creation time,
/// then id).
/// </summary>
/// <param name="Parts">The listings which contribute, in fill order, with the units taken from each.</param>
/// <param name="Quantity">The fillable quantity, at most the requested quantity.</param>
/// <param name="Total">The total price of the fillable quantity in Zen.</param>
public sealed record AuctionGoodsFill(IReadOnlyList<(AuctionIndexedListing Listing, uint Units)> Parts, uint Quantity, ulong Total)
{
    /// <summary>
    /// Compares two listings by fill order: cheapest unit price first, then oldest, then by id.
    /// </summary>
    /// <param name="x">The first listing.</param>
    /// <param name="y">The second listing.</param>
    /// <returns>The comparison result.</returns>
    public static int CompareFillOrder(AuctionIndexedListing x, AuctionIndexedListing y)
    {
        var result = x.UnitPrice.CompareTo(y.UnitPrice);
        if (result == 0)
        {
            result = x.CreatedAt.CompareTo(y.CreatedAt);
        }

        if (result == 0)
        {
            result = x.Id.CompareTo(y.Id);
        }

        return result;
    }

    /// <summary>
    /// Fills a quantity from listings.
    /// </summary>
    /// <param name="listings">The listings; they are put into fill order.</param>
    /// <param name="quantity">The requested quantity.</param>
    /// <returns>The fill, which may have less than the requested quantity.</returns>
    public static AuctionGoodsFill Create(IEnumerable<AuctionIndexedListing> listings, uint quantity)
    {
        var ordered = listings.ToList();
        ordered.Sort(CompareFillOrder);
        var parts = new List<(AuctionIndexedListing Listing, uint Units)>();
        uint filled = 0;
        ulong total = 0;
        foreach (var listing in ordered)
        {
            if (filled >= quantity)
            {
                break;
            }

            var units = Math.Min(listing.RemainingQuantity, quantity - filled);
            if (units == 0)
            {
                continue;
            }

            parts.Add((listing, units));
            filled += units;
            total += (ulong)units * listing.UnitPrice;
        }

        return new AuctionGoodsFill(parts, filled, total);
    }
}
