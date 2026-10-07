// <copyright file="AuctionHouseListingKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The kind of an auction house listing.
/// </summary>
public enum AuctionHouseListingKind : byte
{
    /// <summary>
    /// An individual item.
    /// </summary>
    Item = 1,

    /// <summary>
    /// Goods: identical, interchangeable units, identified by item type and level.
    /// </summary>
    Goods = 2,
}
