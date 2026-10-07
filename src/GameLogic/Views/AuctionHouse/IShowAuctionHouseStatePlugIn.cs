// <copyright file="IShowAuctionHouseStatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Interface of a view whose implementation opens and closes the auction house window.
/// </summary>
public interface IShowAuctionHouseStatePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the auction house window with the rules and the counts of the player.
    /// </summary>
    /// <param name="state">The announced state of the broker.</param>
    ValueTask ShowAuctionHouseOpenedAsync(AuctionBrokerState state);

    /// <summary>
    /// Closes the auction house window.
    /// </summary>
    ValueTask ShowAuctionHouseClosedAsync();
}
