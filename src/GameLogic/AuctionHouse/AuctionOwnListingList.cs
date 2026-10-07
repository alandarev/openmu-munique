// <copyright file="AuctionOwnListingList.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The own listings of a player.
/// </summary>
/// <param name="Result">The result. Anything but <see cref="AuctionHouseResult.Success"/> and <see cref="AuctionHouseResult.Partial"/> means that nothing was charged, moved or created.</param>
/// <param name="Listings">The listings.</param>
public sealed record AuctionOwnListingList(
    AuctionHouseResult Result,
    IReadOnlyList<AuctionOwnListingRow> Listings);
