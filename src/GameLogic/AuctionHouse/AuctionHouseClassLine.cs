// <copyright file="AuctionHouseClassLine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A base character class, including its evolutions, which must be able to use a searched item.
/// </summary>
public enum AuctionHouseClassLine : byte
{
    /// <summary>
    /// Dark Wizard.
    /// </summary>
    DarkWizard = 0,

    /// <summary>
    /// Dark Knight.
    /// </summary>
    DarkKnight = 1,

    /// <summary>
    /// Fairy Elf.
    /// </summary>
    FairyElf = 2,

    /// <summary>
    /// Magic Gladiator.
    /// </summary>
    MagicGladiator = 3,

    /// <summary>
    /// Dark Lord.
    /// </summary>
    DarkLord = 4,

    /// <summary>
    /// Summoner.
    /// </summary>
    Summoner = 5,

    /// <summary>
    /// Rage Fighter.
    /// </summary>
    RageFighter = 6,

    /// <summary>
    /// Any class line.
    /// </summary>
    Any = 255,
}
