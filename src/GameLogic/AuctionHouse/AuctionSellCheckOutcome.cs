// <copyright file="AuctionSellCheckOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Whether and how the item of an inventory slot can be listed.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="InventorySlot">The inventory slot.</param>
/// <param name="Reason">Whether the item is eligible, and if not, why.</param>
/// <param name="Kind">The kind of listing which the item would be.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Available">For goods, the count of identical loose units in the inventory.</param>
/// <param name="LowestUnitPrice">The lowest unit price in Zen of other sellers; 0 for none.</param>
/// <param name="ActiveListings">The count of active listings of the seller.</param>
public sealed record AuctionSellCheckOutcome(
    AuctionHouseResult Result,
    byte InventorySlot,
    AuctionHouseSellCheckReason Reason,
    AuctionHouseListingKind Kind,
    ushort ItemType,
    byte Level,
    uint Available,
    uint LowestUnitPrice,
    byte ActiveListings);
