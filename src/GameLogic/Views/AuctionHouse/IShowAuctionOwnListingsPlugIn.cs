// <copyright file="IShowAuctionOwnListingsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the own auction house listings of the player.
/// </summary>
public interface IShowAuctionOwnListingsPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the own listings.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="listings">The own listings.</param>
    ValueTask ShowAuctionOwnListingsAsync(ushort requestId, AuctionOwnListingList listings);
}
