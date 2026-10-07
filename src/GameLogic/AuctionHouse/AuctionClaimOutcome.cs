// <copyright file="AuctionClaimOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of claiming the auction house collection.
/// </summary>
/// <param name="Result">The result: <see cref="AuctionHouseResult.Success"/>, or <see cref="AuctionHouseResult.Partial"/> when anything was kept back.</param>
/// <param name="CompletedEntries">The count of completed entries.</param>
/// <param name="ItemsMoved">The count of items moved to the inventory.</param>
/// <param name="ZenCollected">The collected Zen.</param>
/// <param name="RemainingEntries">The count of remaining entries.</param>
/// <param name="KeptBack">What kept back a part of the claim.</param>
public sealed record AuctionClaimOutcome(
    AuctionHouseResult Result,
    ushort CompletedEntries,
    uint ItemsMoved,
    uint ZenCollected,
    ushort RemainingEntries,
    AuctionHouseKeptBack KeptBack);
