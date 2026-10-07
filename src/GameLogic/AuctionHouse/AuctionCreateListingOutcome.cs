// <copyright file="AuctionCreateListingOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of creating a listing.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="ListingId">The id of the created listing.</param>
/// <param name="Quantity">The listed quantity.</param>
/// <param name="PostingFee">The charged posting fee in Zen.</param>
public sealed record AuctionCreateListingOutcome(
    AuctionHouseResult Result,
    Guid ListingId,
    uint Quantity,
    uint PostingFee);
