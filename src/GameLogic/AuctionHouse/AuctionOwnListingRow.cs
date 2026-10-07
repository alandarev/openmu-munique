// <copyright file="AuctionOwnListingRow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An own listing of a player.
/// </summary>
/// <param name="ListingId">The id of the listing.</param>
/// <param name="UnitPrice">The unit price in Zen.</param>
/// <param name="Listed">The listed quantity.</param>
/// <param name="Remaining">The remaining quantity.</param>
/// <param name="SecondsLeft">The remaining seconds of the listing.</param>
/// <param name="Kind">The kind of the listing.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Item">The listed item of an item listing; <see langword="null"/> for goods.</param>
public sealed record AuctionOwnListingRow(
    Guid ListingId,
    uint UnitPrice,
    uint Listed,
    uint Remaining,
    uint SecondsLeft,
    AuctionHouseListingKind Kind,
    ushort ItemType,
    byte Level,
    Item? Item);
