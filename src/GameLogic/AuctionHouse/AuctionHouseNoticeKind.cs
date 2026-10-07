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
    /// A listing of the player sold.
    /// </summary>
    Sold = 1,

    /// <summary>
    /// A listing of the player expired.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// Deliveries are waiting in the collection of the player.
    /// </summary>
    DeliveriesWaiting = 3,
}
