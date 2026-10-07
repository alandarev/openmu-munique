// <copyright file="AuctionBuyGoodsOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An order to buy a quantity of goods, all or nothing.
/// </summary>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Quantity">The quantity to buy, at least 1.</param>
/// <param name="ReviewedTotal">The total in Zen which the player reviewed.</param>
public sealed record AuctionBuyGoodsOrder(
    ushort ItemType,
    byte Level,
    uint Quantity,
    uint ReviewedTotal);
