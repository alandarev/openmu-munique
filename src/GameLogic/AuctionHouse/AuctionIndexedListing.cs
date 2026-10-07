// <copyright file="AuctionIndexedListing.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence;

/// <summary>
/// The state of an active listing as of its last commit, as the <see cref="AuctionListingIndex"/> holds it.
/// </summary>
/// <param name="Id">The listing id.</param>
/// <param name="SellerCharacterId">The identifier of the selling character.</param>
/// <param name="SellerName">The name of the selling character.</param>
/// <param name="Kind">The kind.</param>
/// <param name="ItemGroup">The group of the item definition.</param>
/// <param name="ItemNumber">The number of the item definition.</param>
/// <param name="ItemLevel">The item level.</param>
/// <param name="UnitPrice">The unit price in Zen.</param>
/// <param name="ListedQuantity">The listed quantity.</param>
/// <param name="RemainingQuantity">The remaining quantity.</param>
/// <param name="CreatedAt">The point in time (UTC) of the creation.</param>
/// <param name="ExpiresAt">The point in time (UTC) of the expiry.</param>
/// <param name="Item">For item listings, a copy of the escrowed item which no context tracks; otherwise <see langword="null"/>.</param>
public sealed record AuctionIndexedListing(
    Guid Id,
    Guid SellerCharacterId,
    string SellerName,
    AuctionHouseListingKind Kind,
    byte ItemGroup,
    short ItemNumber,
    byte ItemLevel,
    uint UnitPrice,
    uint ListedQuantity,
    uint RemainingQuantity,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Item? Item)
{
    /// <summary>
    /// Gets the item type, <c>group * 512 + number</c>.
    /// </summary>
    public ushort ItemType => AuctionHouseItemTypes.GetItemType(this.ItemGroup, this.ItemNumber);

    /// <summary>
    /// Creates the indexed state of a listing.
    /// </summary>
    /// <param name="listing">The listing.</param>
    /// <returns>The indexed state.</returns>
    public static AuctionIndexedListing Of(AuctionListing listing)
    {
        return new AuctionIndexedListing(
            listing.GetId(),
            listing.SellerCharacterId,
            listing.SellerName,
            listing.Kind,
            listing.ItemGroup,
            listing.ItemNumber,
            listing.ItemLevel,
            (uint)listing.UnitPrice,
            (uint)listing.ListedQuantity,
            (uint)listing.RemainingQuantity,
            listing.CreatedAt,
            listing.ExpiresAt,
            listing.Kind == AuctionHouseListingKind.Item ? listing.Escrow?.Items.FirstOrDefault() : null);
    }
}
