// <copyright file="AuctionBuyGoodsOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of buying a quantity of goods.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Quantity">The bought quantity.</param>
/// <param name="Paid">The paid amount in Zen.</param>
/// <param name="Delivered">The quantity delivered to the inventory.</param>
/// <param name="Kept">The quantity kept in the collection.</param>
public sealed record AuctionBuyGoodsOutcome(
    AuctionHouseResult Result,
    ushort ItemType,
    byte Level,
    uint Quantity,
    uint Paid,
    uint Delivered,
    uint Kept);
