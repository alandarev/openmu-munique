// <copyright file="AuctionGoodsRow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A goods identity of the auction house goods list.
/// </summary>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="LowestUnitPrice">The lowest unit price in Zen.</param>
/// <param name="Available">The available quantity.</param>
/// <param name="ListingCount">The count of listings.</param>
public sealed record AuctionGoodsRow(
    ushort ItemType,
    byte Level,
    uint LowestUnitPrice,
    uint Available,
    ushort ListingCount);
