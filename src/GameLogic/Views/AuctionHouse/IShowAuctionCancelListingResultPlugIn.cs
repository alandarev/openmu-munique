// <copyright file="IShowAuctionCancelListingResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the result of cancelling an auction house listing.
/// </summary>
public interface IShowAuctionCancelListingResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the result of cancelling a listing.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    ValueTask ShowAuctionCancelListingResultAsync(ushort requestId, AuctionCancelListingOutcome outcome);
}
