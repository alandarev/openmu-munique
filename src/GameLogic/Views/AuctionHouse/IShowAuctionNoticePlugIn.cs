// <copyright file="IShowAuctionNoticePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation notifies the player about the auction house.
/// </summary>
public interface IShowAuctionNoticePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a notice of the auction house.
    /// </summary>
    /// <param name="notice">The notice.</param>
    ValueTask ShowAuctionNoticeAsync(AuctionHouseNotice notice);
}
