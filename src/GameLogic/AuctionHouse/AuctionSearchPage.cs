// <copyright file="AuctionSearchPage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A page of an equipment search of the auction house.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="Page">The page, starting at 0.</param>
/// <param name="TotalCount">The total count of matching listings.</param>
/// <param name="Listings">The listings of the page.</param>
public sealed record AuctionSearchPage(
    AuctionHouseResult Result,
    ushort Page,
    uint TotalCount,
    IReadOnlyList<AuctionListingRow> Listings);
