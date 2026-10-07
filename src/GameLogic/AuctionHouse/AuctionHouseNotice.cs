// <copyright file="AuctionHouseNotice.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A notice of the auction house which the server sends on its own.
/// </summary>
/// <param name="Kind">The kind of the notice.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>; 0 for <see cref="AuctionHouseNoticeKind.DeliveriesWaiting"/>.</param>
/// <param name="Level">The item level; 0 for <see cref="AuctionHouseNoticeKind.DeliveriesWaiting"/>.</param>
/// <param name="Quantity">The quantity; 0 for <see cref="AuctionHouseNoticeKind.DeliveriesWaiting"/>.</param>
/// <param name="Net">The net in Zen; 0 for <see cref="AuctionHouseNoticeKind.DeliveriesWaiting"/>.</param>
/// <param name="PendingCollection">The count of entries in the collection of the player.</param>
public sealed record AuctionHouseNotice(
    AuctionHouseNoticeKind Kind,
    ushort ItemType,
    byte Level,
    uint Quantity,
    uint Net,
    ushort PendingCollection);
