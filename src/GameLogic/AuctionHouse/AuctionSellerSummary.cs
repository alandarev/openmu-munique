// <copyright file="AuctionSellerSummary.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The counts of a seller which the broker state announces.
/// </summary>
/// <param name="ActiveListings">The count of active listings of the player.</param>
/// <param name="PendingCollection">The count of entries in the collection of the player.</param>
public sealed record AuctionSellerSummary(
    byte ActiveListings,
    ushort PendingCollection)
{
    /// <summary>
    /// Gets the summary of a player without listings and collection entries.
    /// </summary>
    public static AuctionSellerSummary None { get; } = new(0, 0);
}
