// <copyright file="AuctionHouseItemTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Converts between item definitions and the item type of the auction house protocol, <c>group * 512 + number</c>.
/// </summary>
public static class AuctionHouseItemTypes
{
    private const int NumbersPerGroup = 512;

    /// <summary>
    /// Gets the item type of an item group and number.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <returns>The item type.</returns>
    public static ushort GetItemType(byte group, short number) => (ushort)((group * NumbersPerGroup) + number);

    /// <summary>
    /// Gets the item type of an item definition.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns>The item type.</returns>
    public static ushort GetItemType(ItemDefinition definition) => GetItemType(definition.Group, definition.Number);

    /// <summary>
    /// Gets the item group and number of an item type.
    /// </summary>
    /// <param name="itemType">The item type.</param>
    /// <returns>The group and number.</returns>
    public static (byte Group, short Number) Split(ushort itemType) => ((byte)(itemType / NumbersPerGroup), (short)(itemType % NumbersPerGroup));
}
