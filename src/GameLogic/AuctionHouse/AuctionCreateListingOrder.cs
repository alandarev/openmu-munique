// <copyright file="AuctionCreateListingOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An order to list an item or a quantity of goods of the inventory.
/// </summary>
/// <remarks>
/// The auction house action already checked the quantity, the duration, the price range and the posting fee against the current rules.
/// </remarks>
/// <param name="InventorySlot">The inventory slot of the item; for goods, the identical loose units are taken starting with this slot.</param>
/// <param name="Quantity">The quantity, at least 1; 1 for an item listing.</param>
/// <param name="UnitPrice">The price in Zen of one unit, within the maximum price.</param>
/// <param name="DurationHours">The duration in hours, one of the announced durations.</param>
/// <param name="PostingFee">The posting fee in Zen which the player reviewed; the market gets the order only when it equals the fee under the current rules.</param>
public sealed record AuctionCreateListingOrder(
    byte InventorySlot,
    uint Quantity,
    uint UnitPrice,
    byte DurationHours,
    uint PostingFee);
