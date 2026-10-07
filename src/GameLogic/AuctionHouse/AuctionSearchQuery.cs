// <copyright file="AuctionSearchQuery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The criteria of an equipment search of the auction house. Every given criterion applies.
/// </summary>
/// <param name="Page">The requested page, starting at 0.</param>
/// <param name="Sort">The sort order.</param>
/// <param name="Slots">The equipment slot kinds; <see cref="AuctionHouseEquipmentSlots.None"/> for any.</param>
/// <param name="GroupMask">The native item groups as bits (bit n = item group n); 0 for any.</param>
/// <param name="ClassLine">The class line which must be able to use the item.</param>
/// <param name="Options">The options which the item has to fulfill.</param>
/// <param name="Family">The item family.</param>
/// <param name="UpgradeMin">The minimum item level, 0 to 15.</param>
/// <param name="UpgradeMax">The maximum item level, 0 to 15.</param>
/// <param name="ExcellentMask">The required excellent options, as bits of the excellent option byte of the item; every set bit is required.</param>
/// <param name="MinExcellentCount">The minimum count of excellent options, 0 to 6.</param>
/// <param name="MinSockets">The minimum count of sockets, 0 to 5.</param>
/// <param name="MaxPrice">The maximum price in Zen; 0 for none.</param>
/// <param name="NameMatchTypes">The item types of a name search, at most 48; when given, the type of the item must be one of them.</param>
/// <param name="AncientSetMembers">The ancient set members, at most 16; when given, the item must be ancient with the discriminator and of the type of one of them.</param>
public sealed record AuctionSearchQuery(
    ushort Page,
    AuctionHouseSortOrder Sort,
    AuctionHouseEquipmentSlots Slots,
    ushort GroupMask,
    AuctionHouseClassLine ClassLine,
    AuctionHouseSearchOptions Options,
    AuctionHouseItemFamily Family,
    byte UpgradeMin,
    byte UpgradeMax,
    byte ExcellentMask,
    byte MinExcellentCount,
    byte MinSockets,
    uint MaxPrice,
    IReadOnlyList<ushort> NameMatchTypes,
    IReadOnlyList<AuctionAncientSetMember> AncientSetMembers);
