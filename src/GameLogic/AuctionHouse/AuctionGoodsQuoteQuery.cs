// <copyright file="AuctionGoodsQuoteQuery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A request for the price of a quantity of goods.
/// </summary>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Quantity">The requested quantity, at least 1.</param>
public sealed record AuctionGoodsQuoteQuery(
    ushort ItemType,
    byte Level,
    uint Quantity);
