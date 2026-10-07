// <copyright file="AuctionGoodsPage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A page of the goods list of the auction house, ordered by item type and level.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="Page">The page, starting at 0.</param>
/// <param name="TotalCount">The total count of goods identities.</param>
/// <param name="Goods">The goods of the page.</param>
public sealed record AuctionGoodsPage(
    AuctionHouseResult Result,
    ushort Page,
    uint TotalCount,
    IReadOnlyList<AuctionGoodsRow> Goods);
