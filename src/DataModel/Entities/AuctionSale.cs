// <copyright file="AuctionSale.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The record of a sale of the auction house: units of one listing which one buyer bought at once.
/// </summary>
/// <remarks>
/// Kept for auditing and price history; it references characters and the listing by identifier and name only, so it
/// stays when they are deleted.
/// </remarks>
[AggregateRoot]
public class AuctionSale
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the point in time (UTC) of the sale.
    /// </summary>
    public DateTime SoldAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing.
    /// </summary>
    public Guid ListingId { get; set; }

    /// <summary>
    /// Gets or sets the group of the definition of the sold items.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the number of the definition of the sold items.
    /// </summary>
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the level of the sold items.
    /// </summary>
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the count of sold units.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the price of one unit in Zen.
    /// </summary>
    public long UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the selling character.
    /// </summary>
    public Guid SellerCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the name of the selling character.
    /// </summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the buying character.
    /// </summary>
    public Guid BuyerCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the name of the buying character.
    /// </summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the gross in Zen: the quantity times the unit price.
    /// </summary>
    public long Gross { get; set; }

    /// <summary>
    /// Gets or sets the sale fee in Zen which was withheld from the gross.
    /// </summary>
    public long Fee { get; set; }
}
