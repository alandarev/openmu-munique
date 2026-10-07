// <copyright file="AuctionBuyListingOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An order to buy an item listing.
/// </summary>
/// <param name="ListingId">The id of the listing.</param>
/// <param name="ReviewedPrice">The price in Zen which the player reviewed.</param>
public sealed record AuctionBuyListingOrder(
    Guid ListingId,
    uint ReviewedPrice);
