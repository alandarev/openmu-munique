// <copyright file="AuctionHouseResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The result of an auction house request.
/// </summary>
public enum AuctionHouseResult : byte
{
    /// <summary>
    /// The request succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The auction house is unavailable, e.g. because no market is configured.
    /// </summary>
    Unavailable = 1,

    /// <summary>
    /// The player has no auction house dialog open.
    /// </summary>
    NotOpen = 2,

    /// <summary>
    /// The player sent requests faster than the configured rate.
    /// </summary>
    Busy = 3,

    /// <summary>
    /// The request is invalid.
    /// </summary>
    InvalidRequest = 4,

    /// <summary>
    /// The listing or collection entry was not found.
    /// </summary>
    NotFound = 5,

    /// <summary>
    /// The price or total changed since the player reviewed it.
    /// </summary>
    QuoteChanged = 6,

    /// <summary>
    /// Fewer units are available than requested.
    /// </summary>
    InsufficientStock = 7,

    /// <summary>
    /// The player has not enough Zen.
    /// </summary>
    InsufficientZen = 8,

    /// <summary>
    /// The item is not eligible for listing.
    /// </summary>
    NotEligible = 9,

    /// <summary>
    /// The seller reached the maximum count of active listings.
    /// </summary>
    ListingLimitReached = 10,

    /// <summary>
    /// A price or total is zero or above the maximum price.
    /// </summary>
    PriceOutOfRange = 11,

    /// <summary>
    /// The rules changed since the player reviewed them; a fresh broker state follows.
    /// </summary>
    RulesChanged = 12,

    /// <summary>
    /// The listing is the own listing of the player.
    /// </summary>
    OwnListing = 13,

    /// <summary>
    /// The request succeeded partially; something was kept back.
    /// </summary>
    Partial = 14,

    /// <summary>
    /// An internal error occurred.
    /// </summary>
    InternalError = 15,

    /// <summary>
    /// A goods unit price has more significant digits than the rules allow.
    /// </summary>
    PriceNotOnStep = 16,
}
