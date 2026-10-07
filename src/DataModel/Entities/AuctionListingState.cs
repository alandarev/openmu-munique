// <copyright file="AuctionListingState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The state of an <see cref="AuctionListing"/>.
/// </summary>
public enum AuctionListingState : byte
{
    /// <summary>
    /// The listing offers its remaining units.
    /// </summary>
    Active = 1,

    /// <summary>
    /// All units of the listing were sold.
    /// </summary>
    SoldOut = 2,

    /// <summary>
    /// The seller cancelled the listing; its unsold units were returned.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The duration of the listing ended; its unsold units were returned.
    /// </summary>
    Expired = 4,
}
