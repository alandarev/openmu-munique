// <copyright file="IShowAuctionGoodsListPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the goods list of the auction house.
/// </summary>
public interface IShowAuctionGoodsListPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the goods list.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="page">The page of the goods list.</param>
    ValueTask ShowAuctionGoodsListAsync(ushort requestId, AuctionGoodsPage page);
}
