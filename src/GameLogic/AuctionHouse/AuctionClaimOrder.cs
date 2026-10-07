// <copyright file="AuctionClaimOrder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An order to claim one entry or every entry of the auction house collection.
/// </summary>
/// <param name="EntryId">The id of the entry; <see langword="null"/> for every entry.</param>
public sealed record AuctionClaimOrder(
    Guid? EntryId);
