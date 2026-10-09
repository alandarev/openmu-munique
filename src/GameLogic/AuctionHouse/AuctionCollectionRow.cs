// <copyright file="AuctionCollectionRow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An entry of the auction house collection.
/// </summary>
/// <param name="EntryId">The id of the entry.</param>
/// <param name="Kind">The kind of the entry.</param>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Level">The item level.</param>
/// <param name="Quantity">The quantity.</param>
/// <param name="Gross">The gross in Zen.</param>
/// <param name="Fee">The fee in Zen.</param>
/// <param name="Net">The Zen still to collect.</param>
/// <param name="Item">The individual item, or for the proceeds of an item listing the display copy of the sold item; <see langword="null"/> otherwise.</param>
public sealed record AuctionCollectionRow(
    Guid EntryId,
    AuctionHouseCollectionEntryKind Kind,
    ushort ItemType,
    byte Level,
    uint Quantity,
    uint Gross,
    uint Fee,
    uint Net,
    Item? Item);
