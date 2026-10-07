// <copyright file="AuctionSearchFilter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Numerics;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Evaluates the criteria of an <see cref="AuctionSearchQuery"/> on the escrowed item of a listing.
/// All criteria are combined with AND.
/// </summary>
public static class AuctionSearchFilter
{
    private const byte ShieldGroup = 6;

    /// <summary>
    /// Determines whether an item listing matches the criteria of a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="listing">The item listing.</param>
    /// <param name="player">The requesting player, for <see cref="AuctionHouseSearchOptions.CanEquipNow"/>.</param>
    /// <returns><see langword="true"/>, if the listing matches.</returns>
    public static bool Matches(AuctionSearchQuery query, AuctionIndexedListing listing, Player player)
    {
        if (listing.Item is not { Definition: { } definition } item)
        {
            return false;
        }

        return (query.MaxPrice == 0 || listing.UnitPrice <= query.MaxPrice)
               && (query.Slots == AuctionHouseEquipmentSlots.None || (GetEquipmentSlots(definition) & query.Slots) != 0)
               && (query.GroupMask == 0 || (definition.Group < 16 && (query.GroupMask & (1 << definition.Group)) != 0))
               && (query.ClassLine == AuctionHouseClassLine.Any || IsForClassLine(definition, query.ClassLine))
               && item.Level >= query.UpgradeMin
               && item.Level <= query.UpgradeMax
               && MatchesOptions(query.Options, item, player)
               && MatchesFamily(query.Family, item)
               && MatchesExcellentOptions(query, item)
               && item.SocketCount >= query.MinSockets
               && (query.NameMatchTypes.Count == 0 || query.NameMatchTypes.Contains(listing.ItemType))
               && (query.AncientSetMembers.Count == 0 || IsAncientSetMember(query.AncientSetMembers, listing.ItemType, item));
    }

    /// <summary>
    /// Determines whether the player could equip the item now: the attribute requirements of the item (adjusted by
    /// its level and options) are met and the character class is qualified (<see cref="PlayerItemExtensions.CompliesRequirements"/>).
    /// The slot and the hands which are currently occupied aren't considered.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/>, if the player could equip the item.</returns>
    public static bool CanEquipNow(Player player, Item item)
    {
        return item.Definition?.ItemSlot is not null
               && player.Attributes is not null
               && player.SelectedCharacter?.CharacterClass is not null
               && player.CompliesRequirements(item);
    }

    /// <summary>
    /// Gets the kinds of equipment slots of the protocol in which items of a definition are worn.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns>The equipment slots; <see cref="AuctionHouseEquipmentSlots.None"/> for items which aren't worn.</returns>
    public static AuctionHouseEquipmentSlots GetEquipmentSlots(ItemDefinition definition)
    {
        var result = AuctionHouseEquipmentSlots.None;
        if (definition.ItemSlot is not { } slotType)
        {
            return result;
        }

        foreach (var slot in slotType.ItemSlots)
        {
            result |= slot switch
            {
                0 or 1 when definition.Group == ShieldGroup => AuctionHouseEquipmentSlots.Shield,
                0 or 1 => AuctionHouseEquipmentSlots.Weapon,
                2 => AuctionHouseEquipmentSlots.Helm,
                3 => AuctionHouseEquipmentSlots.Armor,
                4 => AuctionHouseEquipmentSlots.Pants,
                5 => AuctionHouseEquipmentSlots.Gloves,
                6 => AuctionHouseEquipmentSlots.Boots,
                7 => AuctionHouseEquipmentSlots.Wings,
                8 => AuctionHouseEquipmentSlots.Pet,
                9 => AuctionHouseEquipmentSlots.Pendant,
                10 or 11 => AuctionHouseEquipmentSlots.Ring,
                _ => AuctionHouseEquipmentSlots.None,
            };
        }

        return result;
    }

    /// <summary>
    /// Determines whether an item belongs to a family. One item can belong to several families.
    /// </summary>
    /// <param name="family">The family.</param>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/>, if the item belongs to the family.</returns>
    public static bool MatchesFamily(AuctionHouseItemFamily family, Item item)
    {
        return family switch
        {
            AuctionHouseItemFamily.Any => true,
            AuctionHouseItemFamily.Normal => !item.IsExcellent() && !item.IsAncient() && item.SocketCount == 0,
            AuctionHouseItemFamily.Excellent => item.IsExcellent(),
            AuctionHouseItemFamily.Ancient => item.IsAncient(),
            AuctionHouseItemFamily.Socket => item.SocketCount > 0,
            _ => false,
        };
    }

    private static bool IsForClassLine(ItemDefinition definition, AuctionHouseClassLine classLine)
    {
        // The class numbers of the evolutions of a base class share the bits above the lowest two.
        return definition.QualifiedCharacters.Any(characterClass => characterClass.Number >> 2 == (int)classLine);
    }

    private static bool MatchesOptions(AuctionHouseSearchOptions options, Item item, Player player)
    {
        return (!options.HasFlag(AuctionHouseSearchOptions.Luck) || item.ItemOptions.Any(link => link.ItemOption?.OptionType == ItemOptionTypes.Luck))
               && (!options.HasFlag(AuctionHouseSearchOptions.Skill) || item.HasSkill)
               && (!options.HasFlag(AuctionHouseSearchOptions.CanEquipNow) || CanEquipNow(player, item));
    }

    private static bool MatchesExcellentOptions(AuctionSearchQuery query, Item item)
    {
        if (query.ExcellentMask == 0 && query.MinExcellentCount == 0)
        {
            return true;
        }

        var bits = item.GetExcellentOptionBits();
        return (bits & query.ExcellentMask) == query.ExcellentMask
               && BitOperations.PopCount(bits) >= query.MinExcellentCount;
    }

    private static bool IsAncientSetMember(IReadOnlyList<AuctionAncientSetMember> members, ushort itemType, Item item)
    {
        return members.Any(member => member.ItemType == itemType
                                     && item.ItemSetGroups.Any(set => set.AncientSetDiscriminator == member.Discriminator));
    }
}
