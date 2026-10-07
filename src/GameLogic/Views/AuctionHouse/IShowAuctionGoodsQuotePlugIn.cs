// <copyright file="IShowAuctionGoodsQuotePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the price of a quantity of goods of the auction house.
/// </summary>
public interface IShowAuctionGoodsQuotePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the price of a quantity of goods.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="quotation">The quotation.</param>
    ValueTask ShowAuctionGoodsQuoteAsync(ushort requestId, AuctionGoodsQuotation quotation);
}
