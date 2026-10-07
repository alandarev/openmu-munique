// <copyright file="AuctionHouseSortOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The sort order of an auction house search.
/// </summary>
public enum AuctionHouseSortOrder : byte
{
    /// <summary>
    /// The cheapest listing first.
    /// </summary>
    PriceAscending = 0,

    /// <summary>
    /// The most expensive listing first.
    /// </summary>
    PriceDescending = 1,
}
