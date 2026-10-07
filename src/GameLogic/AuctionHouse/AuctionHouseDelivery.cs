// <copyright file="AuctionHouseDelivery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Where a bought item went.
/// </summary>
public enum AuctionHouseDelivery : byte
{
    /// <summary>
    /// Nothing was delivered.
    /// </summary>
    None = 0,

    /// <summary>
    /// To the inventory.
    /// </summary>
    Inventory = 1,

    /// <summary>
    /// To the collection, because it did not fit into the inventory.
    /// </summary>
    Collection = 2,
}
