// <copyright file="AuctionListingRow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An item listing of an auction house search result.
/// </summary>
/// <param name="ListingId">The id of the listing.</param>
/// <param name="Price">The price in Zen.</param>
/// <param name="SecondsLeft">The remaining seconds of the listing.</param>
/// <param name="SellerName">The character name of the seller.</param>
/// <param name="RequirementsMet">A value indicating whether the requesting character meets the requirements of the item.</param>
/// <param name="IsOwnListing">A value indicating whether it is the own listing of the requesting player.</param>
/// <param name="Item">The listed item.</param>
public sealed record AuctionListingRow(
    Guid ListingId,
    uint Price,
    uint SecondsLeft,
    string SellerName,
    bool RequirementsMet,
    bool IsOwnListing,
    Item Item);
