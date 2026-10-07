// <copyright file="AuctionHouseNoticeKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The kind of an auction house notice.
/// </summary>
public enum AuctionHouseNoticeKind : byte
{
    /// <summary>
    /// A listing of the player sold; the proceeds are in the collection.
    /// </summary>
    Sold = 1,

    /// <summary>
    /// A listing of the player expired; the units are in the collection.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// Deliveries are waiting in the collection of the player.
    /// </summary>
    DeliveriesWaiting = 3,

    /// <summary>
    /// A listing of the player sold; the proceeds were added to the inventory Zen, because the player was at a broker.
    /// </summary>
    SoldToInventory = 4,

    /// <summary>
    /// A listing of the player expired; the units were returned to the inventory, because the player was at a broker.
    /// </summary>
    ExpiredToInventory = 5,
}
