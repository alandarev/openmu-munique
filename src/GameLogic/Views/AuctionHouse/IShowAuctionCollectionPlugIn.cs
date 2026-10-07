// <copyright file="IShowAuctionCollectionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation informs about the auction house collection of the player.
/// </summary>
public interface IShowAuctionCollectionPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a page of the collection.
    /// </summary>
    /// <param name="requestId">The request id of the answered request.</param>
    /// <param name="page">The page of the collection.</param>
    ValueTask ShowAuctionCollectionAsync(ushort requestId, AuctionCollectionPage page);
}
