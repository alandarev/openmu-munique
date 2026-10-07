// <copyright file="AuctionTestCharacter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// A character with a new identifier and plain collections, so that tests can give players distinct characters whose
/// inventory belongs to a persistence context.
/// </summary>
internal sealed class AuctionTestCharacter : Character
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionTestCharacter"/> class.
    /// </summary>
    public AuctionTestCharacter()
    {
        this.Attributes = new List<StatAttribute>();
        this.LearnedSkills = new List<SkillEntry>();
        this.DropItemGroups = new List<DropItemGroup>();
        this.QuestStates = new List<CharacterQuestState>();
        this.Id = Guid.NewGuid();
    }
}
