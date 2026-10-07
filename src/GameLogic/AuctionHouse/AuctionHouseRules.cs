// <copyright file="AuctionHouseRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// The rules of the auction house, which the broker announces when it opens.
/// It's the custom configuration of the <see cref="AuctionHouseFeaturePlugIn"/>.
/// </summary>
/// <remarks>
/// The announced inventory Zen limit is not part of these rules, it's
/// <see cref="DataModel.Configuration.GameConfiguration.MaximumInventoryMoney"/>.
/// </remarks>
public class AuctionHouseRules
{
    /// <summary>
    /// The maximum count of offered listing durations.
    /// </summary>
    public const int MaximumDurationCount = 4;

    /// <summary>
    /// Gets the default rules, which apply when the auction house feature has no configuration.
    /// </summary>
    public static AuctionHouseRules Default => new();

    /// <summary>
    /// Gets or sets the count of rows of a page.
    /// </summary>
    public byte PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum count of active listings of a seller.
    /// </summary>
    public byte MaximumActiveListings { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum price or total in Zen. The announced maximum is at most the inventory Zen limit.
    /// </summary>
    public uint MaximumPrice { get; set; } = 2_000_000_000;

    /// <summary>
    /// Gets or sets the flat part of the posting fee in Zen.
    /// </summary>
    public uint PostingFeeFlat { get; set; }

    /// <summary>
    /// Gets or sets the part of the posting fee in basis points (1/10000) of the listing total.
    /// </summary>
    public ushort PostingFeeBasisPoints { get; set; } = 500;

    /// <summary>
    /// Gets or sets the sale fee in basis points (1/10000) of the gross of each sale.
    /// </summary>
    public ushort SaleFeeBasisPoints { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets a value indicating whether the posting fee is refunded when a listing is cancelled.
    /// </summary>
    public bool RefundPostingFeeOnCancel { get; set; }

    /// <summary>
    /// Gets or sets the offered listing durations in hours; one to <see cref="MaximumDurationCount"/> of them.
    /// </summary>
    public List<byte> DurationHours { get; set; } = [48];

    /// <summary>
    /// Gets or sets the count of significant digits which a goods unit price may have, so that sellers can't
    /// undercut each other by a single Zen; 0 means no restriction. See <see cref="AuctionHouseFees.IsOnPriceStep"/>.
    /// </summary>
    public byte GoodsPriceSignificantDigits { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum count of auction house requests per second of a player;
    /// further requests are answered with <see cref="AuctionHouseResult.Busy"/>. 0 means no limit.
    /// </summary>
    public byte MaximumRequestsPerSecond { get; set; } = 5;

    /// <summary>
    /// Gets or sets the item types which are traded as goods; every other eligible item is listed individually.
    /// By default the jewels of Bless, Soul, Life, Creation, Guardian, Chaos and Harmony, the Gemstone, the lower and
    /// higher refining stones, and the packed form of each.
    /// </summary>
    public List<AuctionGoodsItem> Goods { get; set; } =
    [
        new() { Group = 14, Number = 13, Name = "Jewel of Bless" },
        new() { Group = 14, Number = 14, Name = "Jewel of Soul" },
        new() { Group = 14, Number = 16, Name = "Jewel of Life" },
        new() { Group = 14, Number = 22, Name = "Jewel of Creation" },
        new() { Group = 14, Number = 31, Name = "Jewel of Guardian" },
        new() { Group = 12, Number = 15, Name = "Jewel of Chaos" },
        new() { Group = 14, Number = 42, Name = "Jewel of Harmony" },
        new() { Group = 14, Number = 41, Name = "Gemstone" },
        new() { Group = 14, Number = 43, Name = "Lower refine stone" },
        new() { Group = 14, Number = 44, Name = "Higher refine stone" },
        new() { Group = 12, Number = 30, Name = "Packed Jewel of Bless" },
        new() { Group = 12, Number = 31, Name = "Packed Jewel of Soul" },
        new() { Group = 12, Number = 136, Name = "Packed Jewel of Life" },
        new() { Group = 12, Number = 137, Name = "Packed Jewel of Creation" },
        new() { Group = 12, Number = 138, Name = "Packed Jewel of Guardian" },
        new() { Group = 12, Number = 141, Name = "Packed Jewel of Chaos" },
        new() { Group = 12, Number = 140, Name = "Packed Jewel of Harmony" },
        new() { Group = 12, Number = 139, Name = "Packed Gemstone" },
        new() { Group = 12, Number = 142, Name = "Packed Lower refine stone" },
        new() { Group = 12, Number = 143, Name = "Packed Higher refine stone" },
    ];

    /// <summary>
    /// Determines whether items of the given definition are traded as goods.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><see langword="true"/> if the items are goods.</returns>
    public bool IsGoods(ItemDefinition definition)
    {
        return this.Goods?.Any(goods => goods.Group == definition.Group && goods.Number == definition.Number) ?? false;
    }

    /// <summary>
    /// Gets the offered listing durations in hours, as they are announced: the first
    /// <see cref="MaximumDurationCount"/> durations which are not 0.
    /// </summary>
    /// <returns>The offered listing durations in hours.</returns>
    public IReadOnlyList<byte> GetOfferedDurations()
    {
        return (this.DurationHours ?? []).Where(hours => hours > 0).Take(MaximumDurationCount).ToList();
    }
}
