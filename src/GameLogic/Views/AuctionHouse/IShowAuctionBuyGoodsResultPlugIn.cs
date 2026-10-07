// <copyright file="IShowAuctionBuyGoodsResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the result of buying auction house goods.
/// </summary>
public interface IShowAuctionBuyGoodsResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the result of buying goods.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    ValueTask ShowAuctionBuyGoodsResultAsync(ushort requestId, AuctionBuyGoodsOutcome outcome);
}
