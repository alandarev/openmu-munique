// <copyright file="AuctionCollectionEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

using MUnique.OpenMU.Annotations;

/// <summary>
/// An entry of a character's auction house collection: bought items, returned units or proceeds which wait until the
/// character collects them. The items are held in the <see cref="Escrow"/>.
/// </summary>
/// <remarks>
/// The owner is referenced by its identifier only, so that loading a character doesn't load its collection.
/// Deleting the character deletes its entries with their items.
/// </remarks>
[AggregateRoot]
public class AuctionCollectionEntry
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the character which receives the entry.
    /// </summary>
    public Guid OwnerCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public AuctionHouseCollectionEntryKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing which the entry results from; the proceeds of a listing accumulate
    /// in one entry.
    /// </summary>
    public Guid ListingId { get; set; }

    /// <summary>
    /// Gets or sets the group of the definition of the items.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the number of the definition of the items.
    /// </summary>
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the level of the items.
    /// </summary>
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the count of units: bought or returned units, or the sold units of proceeds.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the gross of proceeds in Zen, before the sale fee.
    /// </summary>
    public long Gross { get; set; }

    /// <summary>
    /// Gets or sets the sale fee in Zen which was withheld from the <see cref="Gross"/>.
    /// </summary>
    public long Fee { get; set; }

    /// <summary>
    /// Gets or sets the Zen which is still to be collected.
    /// </summary>
    public long UncollectedMoney { get; set; }

    /// <summary>
    /// Gets or sets the point in time (UTC) when the entry was created or last received units or Zen.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the storage which holds the items which are still to be collected.
    /// </summary>
    [MemberOfAggregate]
    public virtual ItemStorage? Escrow { get; set; }
}
