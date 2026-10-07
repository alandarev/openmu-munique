// <copyright file="AuctionGoodsListQuery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The criteria of the goods list of the auction house.
/// </summary>
/// <param name="Page">The requested page, starting at 0.</param>
/// <param name="ItemTypes">The item types to which the list is limited, at most 64; empty for all goods.</param>
public sealed record AuctionGoodsListQuery(
    ushort Page,
    IReadOnlyList<ushort> ItemTypes);
