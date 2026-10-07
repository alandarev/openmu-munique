// <copyright file="AuctionHouseSortOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The sort order of an auction house search. Ties are broken by listing time ascending, then by listing id.
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

    /// <summary>
    /// The listing which ends soonest first.
    /// </summary>
    EndingSoon = 2,

    /// <summary>
    /// The newest listing first.
    /// </summary>
    Newest = 3,
}
