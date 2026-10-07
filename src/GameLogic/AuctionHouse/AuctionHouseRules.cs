// <copyright file="AuctionHouseRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

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
    public uint PostingFeeFlat { get; set; } = 30_000;

    /// <summary>
    /// Gets or sets the part of the posting fee in basis points (1/10000) of the listing total.
    /// </summary>
    public ushort PostingFeeBasisPoints { get; set; }

    /// <summary>
    /// Gets or sets the sale fee in basis points (1/10000) of the gross of each sale.
    /// </summary>
    public ushort SaleFeeBasisPoints { get; set; } = 200;

    /// <summary>
    /// Gets or sets a value indicating whether the posting fee is refunded when a listing is cancelled.
    /// </summary>
    public bool RefundPostingFeeOnCancel { get; set; }

    /// <summary>
    /// Gets or sets the offered listing durations in hours; one to <see cref="MaximumDurationCount"/> of them.
    /// </summary>
    public List<byte> DurationHours { get; set; } = [12, 24, 48, 72];

    /// <summary>
    /// Gets or sets the maximum count of auction house requests per second of a player;
    /// further requests are answered with <see cref="AuctionHouseResult.Busy"/>. 0 means no limit.
    /// </summary>
    public byte MaximumRequestsPerSecond { get; set; } = 5;

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
