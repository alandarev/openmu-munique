// <copyright file="AuctionHouseCollectionEntryKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The kind of an entry of the auction house collection.
/// </summary>
public enum AuctionHouseCollectionEntryKind : byte
{
    /// <summary>
    /// A purchase which did not fit into the inventory.
    /// </summary>
    Purchase = 1,

    /// <summary>
    /// The proceeds of a listing.
    /// </summary>
    Proceeds = 2,

    /// <summary>
    /// The units of a cancelled listing.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The units of an expired listing.
    /// </summary>
    Expired = 4,
}
