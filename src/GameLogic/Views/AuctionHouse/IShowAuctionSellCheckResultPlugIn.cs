// <copyright file="IShowAuctionSellCheckResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs whether an item can be listed in the auction house.
/// </summary>
public interface IShowAuctionSellCheckResultPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows whether and how an item can be listed.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="outcome">The outcome.</param>
    ValueTask ShowAuctionSellCheckResultAsync(ushort requestId, AuctionSellCheckOutcome outcome);
}
