// <copyright file="AuctionCollectionPage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A page of the auction house collection.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="Page">The page, starting at 0.</param>
/// <param name="TotalCount">The total count of entries.</param>
/// <param name="Entries">The entries of the page.</param>
public sealed record AuctionCollectionPage(
    AuctionHouseResult Result,
    ushort Page,
    ushort TotalCount,
    IReadOnlyList<AuctionCollectionRow> Entries);
