// <copyright file="AuctionSellerDeliveryOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A committed collection entry which is to be delivered to its owner, a seller, with the notice which tells about it.
/// </summary>
/// <param name="CharacterId">The identifier of the seller's character.</param>
/// <param name="CharacterName">The name of the seller's character.</param>
/// <param name="EntryId">The identifier of the collection entry.</param>
/// <param name="DeliveredKind">The notice kind when the entry was delivered to the inventory in full.</param>
/// <param name="WaitingKind">The notice kind when the entry (or a part of it) waits in the collection.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Quantity">The sold or returned quantity.</param>
/// <param name="Net">The proceeds in Zen after the sale fee; 0 for returned units.</param>
internal sealed record AuctionSellerDeliveryOrder(
    Guid CharacterId,
    string CharacterName,
    Guid EntryId,
    AuctionHouseNoticeKind DeliveredKind,
    AuctionHouseNoticeKind WaitingKind,
    ushort ItemType,
    byte Level,
    uint Quantity,
    uint Net);
