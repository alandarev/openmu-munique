// <copyright file="AuctionHouseItemFamily.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The item family of an auction house search.
/// </summary>
/// <remarks>
/// The family is tested on the item, so one item can match several families.
/// </remarks>
public enum AuctionHouseItemFamily : byte
{
    /// <summary>
    /// Any item.
    /// </summary>
    Any = 0,

    /// <summary>
    /// An item without excellent option, which is not ancient and has no socket.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// An item with any excellent option.
    /// </summary>
    Excellent = 2,

    /// <summary>
    /// An item of an ancient set.
    /// </summary>
    Ancient = 3,

    /// <summary>
    /// An item with at least one socket.
    /// </summary>
    Socket = 4,
}
