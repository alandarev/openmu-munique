// <copyright file="AuctionGoodsItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An item type which the auction house trades as goods: identical, interchangeable units, identified by the item
/// type and level. See <see cref="AuctionHouseRules.Goods"/>.
/// </summary>
public class AuctionGoodsItem
{
    /// <summary>
    /// Gets or sets the group of the item definition.
    /// </summary>
    public byte Group { get; set; }

    /// <summary>
    /// Gets or sets the number of the item definition.
    /// </summary>
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the name, as a hint for the configuration only.
    /// </summary>
    public string? Name { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{this.Name} ({this.Group}, {this.Number})";
}
