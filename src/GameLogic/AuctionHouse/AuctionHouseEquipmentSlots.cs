// <copyright file="AuctionHouseEquipmentSlots.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Equipment slot kinds of an auction house search.
/// </summary>
[Flags]
public enum AuctionHouseEquipmentSlots : ushort
{
    /// <summary>
    /// Any slot.
    /// </summary>
    None = 0,

    /// <summary>
    /// A weapon.
    /// </summary>
    Weapon = 1 << 0,

    /// <summary>
    /// A shield.
    /// </summary>
    Shield = 1 << 1,

    /// <summary>
    /// A helm.
    /// </summary>
    Helm = 1 << 2,

    /// <summary>
    /// An armor.
    /// </summary>
    Armor = 1 << 3,

    /// <summary>
    /// Pants.
    /// </summary>
    Pants = 1 << 4,

    /// <summary>
    /// Gloves.
    /// </summary>
    Gloves = 1 << 5,

    /// <summary>
    /// Boots.
    /// </summary>
    Boots = 1 << 6,

    /// <summary>
    /// Wings.
    /// </summary>
    Wings = 1 << 7,

    /// <summary>
    /// A pendant.
    /// </summary>
    Pendant = 1 << 8,

    /// <summary>
    /// A ring.
    /// </summary>
    Ring = 1 << 9,

    /// <summary>
    /// A pet.
    /// </summary>
    Pet = 1 << 10,
}
