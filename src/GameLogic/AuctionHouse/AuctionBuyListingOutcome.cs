// <copyright file="AuctionBuyListingOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of buying an item listing.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="ListingId">The id of the listing.</param>
/// <param name="Paid">The paid amount in Zen.</param>
/// <param name="Delivery">Where the item went.</param>
public sealed record AuctionBuyListingOutcome(
    AuctionHouseResult Result,
    Guid ListingId,
    uint Paid,
    AuctionHouseDelivery Delivery);
