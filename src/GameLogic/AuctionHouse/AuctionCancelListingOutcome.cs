// <copyright file="AuctionCancelListingOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of cancelling a listing.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="ListingId">The id of the listing.</param>
/// <param name="ToInventory">The quantity returned to the inventory.</param>
/// <param name="ToCollection">The quantity moved to the collection.</param>
/// <param name="RefundedFee">The refunded posting fee in Zen.</param>
public sealed record AuctionCancelListingOutcome(
    AuctionHouseResult Result,
    Guid ListingId,
    uint ToInventory,
    uint ToCollection,
    uint RefundedFee);
