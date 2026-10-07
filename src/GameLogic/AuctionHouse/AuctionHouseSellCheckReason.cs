// <copyright file="AuctionHouseSellCheckReason.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Whether an item is eligible for listing, and if not, why.
/// </summary>
public enum AuctionHouseSellCheckReason : byte
{
    /// <summary>
    /// The item is eligible.
    /// </summary>
    Eligible = 0,

    /// <summary>
    /// The item is not tradable.
    /// </summary>
    NotTradable = 1,

    /// <summary>
    /// There is no item in the inventory slot.
    /// </summary>
    NoItem = 2,

    /// <summary>
    /// The item is not accepted by the market.
    /// </summary>
    NotAccepted = 3,
}
