// <copyright file="AuctionGoodsQuotation.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The price of a quantity of goods, filled cheapest first.
/// </summary>
/// <param name="Result">The result: <see cref="AuctionHouseResult.Success"/> when the requested quantity is fillable, <see cref="AuctionHouseResult.InsufficientStock"/> when only less is available, <see cref="AuctionHouseResult.PriceOutOfRange"/> when the total would exceed the maximum price.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="RequestedQuantity">The requested quantity.</param>
/// <param name="FillableQuantity">The fillable quantity, at most the requested quantity.</param>
/// <param name="Total">The total in Zen for the fillable quantity; 0 with <see cref="AuctionHouseResult.PriceOutOfRange"/>.</param>
/// <param name="Tiers">The cheapest price tiers, at most 8, cheapest first.</param>
public sealed record AuctionGoodsQuotation(
    AuctionHouseResult Result,
    ushort ItemType,
    byte Level,
    uint RequestedQuantity,
    uint FillableQuantity,
    uint Total,
    IReadOnlyList<AuctionPriceTier> Tiers);
