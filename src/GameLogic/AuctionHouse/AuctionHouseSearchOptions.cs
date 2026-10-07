// <copyright file="AuctionHouseSearchOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Options of an auction house search which an item has to fulfill.
/// </summary>
[Flags]
public enum AuctionHouseSearchOptions : byte
{
    /// <summary>
    /// No option.
    /// </summary>
    None = 0,

    /// <summary>
    /// The requesting character can equip the item now, by the equip rules of the server.
    /// </summary>
    CanEquipNow = 1,

    /// <summary>
    /// The item has luck.
    /// </summary>
    Luck = 2,

    /// <summary>
    /// The item has a skill.
    /// </summary>
    Skill = 4,
}
