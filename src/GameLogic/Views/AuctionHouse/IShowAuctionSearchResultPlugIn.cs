// <copyright file="IShowAuctionSearchResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the result of an auction house equipment search.
/// </summary>
public interface IShowAuctionSearchResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the result of an equipment search.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="page">The page of the search.</param>
    ValueTask ShowAuctionSearchResultAsync(ushort requestId, AuctionSearchPage page);
}
