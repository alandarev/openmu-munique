// <copyright file="AuctionBrokerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The announced state of an open auction house broker: the market rules and the counts of the player.
/// </summary>
/// <param name="PageSize">The count of rows of a page.</param>
/// <param name="MaximumActiveListings">The maximum count of active listings of a seller.</param>
/// <param name="ActiveListings">The count of active listings of the player.</param>
/// <param name="PendingCollection">The count of entries in the collection of the player.</param>
/// <param name="ZenLimit">The inventory Zen limit, at most <see cref="int.MaxValue"/>.</param>
/// <param name="MaximumPrice">The maximum price or total in Zen, at most <paramref name="ZenLimit"/>.</param>
/// <param name="PostingFeeFlat">The flat part of the posting fee in Zen.</param>
/// <param name="PostingFeeBasisPoints">The part of the posting fee in basis points of the listing total.</param>
/// <param name="SaleFeeBasisPoints">The sale fee in basis points of the gross of each sale.</param>
/// <param name="RefundPostingFeeOnCancel">A value indicating whether the posting fee is refunded when a listing is cancelled.</param>
/// <param name="DurationHours">The offered listing durations in hours, at most <see cref="AuctionHouseRules.MaximumDurationCount"/>.</param>
/// <param name="GoodsPriceSignificantDigits">The count of significant digits which a goods unit price may have; 0 means no restriction.</param>
public sealed record AuctionBrokerState(
    byte PageSize,
    byte MaximumActiveListings,
    byte ActiveListings,
    ushort PendingCollection,
    uint ZenLimit,
    uint MaximumPrice,
    uint PostingFeeFlat,
    ushort PostingFeeBasisPoints,
    ushort SaleFeeBasisPoints,
    bool RefundPostingFeeOnCancel,
    IReadOnlyList<byte> DurationHours,
    byte GoodsPriceSignificantDigits)
{
    /// <summary>
    /// Creates the announced state for the rules, the inventory Zen limit of the game and the counts of a player.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="maximumInventoryMoney">The inventory Zen limit of the game configuration.</param>
    /// <param name="summary">The counts of the player.</param>
    /// <returns>The announced state.</returns>
    public static AuctionBrokerState Create(AuctionHouseRules rules, int maximumInventoryMoney, AuctionSellerSummary summary)
    {
        var zenLimit = AuctionHouseFees.GetZenLimit(maximumInventoryMoney);
        return new AuctionBrokerState(
            rules.PageSize,
            rules.MaximumActiveListings,
            summary.ActiveListings,
            summary.PendingCollection,
            zenLimit,
            Math.Min(rules.MaximumPrice, zenLimit),
            rules.PostingFeeFlat,
            rules.PostingFeeBasisPoints,
            rules.SaleFeeBasisPoints,
            rules.RefundPostingFeeOnCancel,
            rules.GetOfferedDurations(),
            rules.GoodsPriceSignificantDigits);
    }
}
