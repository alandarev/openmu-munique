// <copyright file="AuctionCancelListingOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An order to cancel an own active listing.
/// </summary>
/// <param name="ListingId">The id of the listing.</param>
public sealed record AuctionCancelListingOrder(
    Guid ListingId);
