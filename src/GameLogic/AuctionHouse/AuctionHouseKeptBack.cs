// <copyright file="AuctionHouseKeptBack.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// What kept back a part of a claim of the auction house collection.
/// </summary>
[Flags]
public enum AuctionHouseKeptBack : byte
{
    /// <summary>
    /// Nothing was kept back.
    /// </summary>
    None = 0,

    /// <summary>
    /// Items were kept back because the inventory was full.
    /// </summary>
    InventorySpace = 1,

    /// <summary>
    /// Zen was kept back because of the inventory Zen limit.
    /// </summary>
    ZenLimit = 2,
}
