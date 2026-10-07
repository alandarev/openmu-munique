// <copyright file="AuctionHouseExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Extensions.ModelBuilder;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Extensions for the <see cref="EntityTypeBuilder{T}"/> of the auction house entities.
/// </summary>
/// <remarks>
/// Listings and collection entries reference their character by identifier, with a cascading foreign key, so that the
/// character's graph doesn't include them. Their escrow storage is the principal of the generated one-to-one
/// relationship, so deleting a character can't reach it by a foreign key; the migration <c>AddAuctionHouse</c>
/// therefore adds a trigger which deletes the escrow storages (and by that the listings, entries and items) of a
/// character before the character is deleted.
/// </remarks>
internal static class AuctionHouseExtensions
{
    /// <summary>
    /// Applies the settings for the <see cref="AuctionListing"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionListing> builder)
    {
        builder.Property(listing => listing.SellerName).HasMaxLength(10).IsRequired();
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(listing => listing.SellerCharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(listing => listing.SellerCharacterId);
        builder.HasIndex(listing => new { listing.State, listing.ExpiresAt });
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionCollectionEntry"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionCollectionEntry> builder)
    {
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(entry => entry.OwnerCharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entry => entry.OwnerCharacterId);
        builder.HasIndex(entry => entry.ListingId);
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionSale"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionSale> builder)
    {
        builder.Property(sale => sale.SellerName).HasMaxLength(10).IsRequired();
        builder.Property(sale => sale.BuyerName).HasMaxLength(10).IsRequired();
        builder.HasIndex(sale => sale.ListingId);
        builder.HasIndex(sale => new { sale.ItemGroup, sale.ItemNumber, sale.ItemLevel, sale.SoldAt });
    }
}
