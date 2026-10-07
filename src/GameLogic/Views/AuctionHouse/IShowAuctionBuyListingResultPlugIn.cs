// <copyright file="IShowAuctionBuyListingResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the result of buying an auction house listing.
/// </summary>
public interface IShowAuctionBuyListingResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the result of buying a listing.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    ValueTask ShowAuctionBuyListingResultAsync(ushort requestId, AuctionBuyListingOutcome outcome);
}
