// <copyright file="AuctionSellCheckQuery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A request whether the item of an inventory slot can be listed.
/// </summary>
/// <param name="InventorySlot">The inventory slot.</param>
public sealed record AuctionSellCheckQuery(
    byte InventorySlot);
