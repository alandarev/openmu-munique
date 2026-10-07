// <copyright file="AuctionListing.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

using MUnique.OpenMU.Annotations;

/// <summary>
/// A listing of the auction house: units of one item type and level which a character offers for a fixed unit price.
/// The offered items are held in the <see cref="Escrow"/> until they are sold, cancelled or expired.
/// </summary>
/// <remarks>
/// The seller is referenced by its identifier only, so that loading a character doesn't load its listings.
/// Deleting the character deletes its listings with their escrowed items.
/// </remarks>
[AggregateRoot]
public class AuctionListing
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the selling character.
    /// </summary>
    public Guid SellerCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the name of the selling character.
    /// </summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public AuctionHouseListingKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the group of the definition of the listed items.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the number of the definition of the listed items.
    /// </summary>
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the level of the listed items.
    /// </summary>
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the price of one unit in Zen.
    /// </summary>
    public long UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the count of units which were listed.
    /// </summary>
    public int ListedQuantity { get; set; }

    /// <summary>
    /// Gets or sets the count of units which are not sold yet; held in the <see cref="Escrow"/> while the listing is
    /// <see cref="AuctionListingState.Active"/>.
    /// </summary>
    public int RemainingQuantity { get; set; }

    /// <summary>
    /// Gets or sets the posting fee in Zen which the seller paid.
    /// </summary>
    public long PostingFee { get; set; }

    /// <summary>
    /// Gets or sets the point in time (UTC) when the listing was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the point in time (UTC) when the listing expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AuctionListingState State { get; set; }

    /// <summary>
    /// Gets or sets the storage which holds the unsold items of the listing.
    /// </summary>
    [MemberOfAggregate]
    public virtual ItemStorage? Escrow { get; set; }
}
