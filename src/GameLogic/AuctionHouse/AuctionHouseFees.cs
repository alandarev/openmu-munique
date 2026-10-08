// <copyright file="AuctionHouseFees.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The Zen amounts and fees of the auction house.
/// </summary>
/// <remarks>
/// Every amount is a whole number of Zen. Products are computed with 64-bit intermediates,
/// so no combination of 32-bit quantities, prices and basis points overflows.
/// </remarks>
public static class AuctionHouseFees
{
    /// <summary>
    /// The basis points of 100 percent.
    /// </summary>
    public const uint BasisPointsPerWhole = 10_000;

    /// <summary>
    /// Gets the announced inventory Zen limit for the configured maximum inventory money: between 0 and <see cref="int.MaxValue"/>.
    /// </summary>
    /// <param name="maximumInventoryMoney">The configured maximum inventory money.</param>
    /// <returns>The inventory Zen limit.</returns>
    public static uint GetZenLimit(int maximumInventoryMoney) => (uint)Math.Max(0, maximumInventoryMoney);

    /// <summary>
    /// Tries to calculate the total of a quantity at a unit price, which has to be between 1 and the maximum price.
    /// </summary>
    /// <param name="quantity">The quantity, at least 1.</param>
    /// <param name="unitPrice">The unit price in Zen, at least 1.</param>
    /// <param name="maximumPrice">The maximum price or total in Zen.</param>
    /// <param name="total">The total in Zen.</param>
    /// <returns><see langword="true"/>, if quantity and unit price are at least 1 and the total is at most the maximum price; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetTotal(uint quantity, uint unitPrice, uint maximumPrice, out uint total)
    {
        var exactTotal = (ulong)quantity * unitPrice;
        if (quantity == 0 || unitPrice == 0 || exactTotal > maximumPrice)
        {
            total = 0;
            return false;
        }

        total = (uint)exactTotal;
        return true;
    }

    /// <summary>
    /// Calculates the floored basis points of an amount: <c>floor(amount * basisPoints / 10000)</c>.
    /// </summary>
    /// <param name="amount">The amount in Zen.</param>
    /// <param name="basisPoints">The basis points.</param>
    /// <returns>The floored part of the amount; it can exceed <see cref="uint.MaxValue"/> for more than 10000 basis points.</returns>
    public static ulong GetBasisPointsOf(uint amount, ushort basisPoints) => (ulong)amount * basisPoints / BasisPointsPerWhole;

    /// <summary>
    /// Tries to calculate the posting fee of a listing: <c>flat + floor(quantity * unitPrice * basisPoints / 10000)</c>.
    /// </summary>
    /// <param name="postingFeeFlat">The flat part of the posting fee in Zen.</param>
    /// <param name="postingFeeBasisPoints">The basis points of the listing total.</param>
    /// <param name="quantity">The quantity, at least 1.</param>
    /// <param name="unitPrice">The unit price in Zen, at least 1.</param>
    /// <param name="maximumPrice">The maximum price or total in Zen.</param>
    /// <param name="postingFee">The posting fee in Zen.</param>
    /// <returns><see langword="true"/>, if the listing total is valid (see <see cref="TryGetTotal"/>) and the posting fee is at most the maximum price; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPostingFee(uint postingFeeFlat, ushort postingFeeBasisPoints, uint quantity, uint unitPrice, uint maximumPrice, out uint postingFee)
    {
        postingFee = 0;
        if (!TryGetTotal(quantity, unitPrice, maximumPrice, out var total))
        {
            return false;
        }

        var fee = postingFeeFlat + GetBasisPointsOf(total, postingFeeBasisPoints);
        if (fee > maximumPrice)
        {
            return false;
        }

        postingFee = (uint)fee;
        return true;
    }

    /// <summary>
    /// Tries to calculate the posting fee of a listing under the rules of an announced broker state.
    /// </summary>
    /// <param name="state">The announced broker state.</param>
    /// <param name="quantity">The quantity, at least 1.</param>
    /// <param name="unitPrice">The unit price in Zen, at least 1.</param>
    /// <param name="postingFee">The posting fee in Zen.</param>
    /// <returns><see langword="true"/>, if the listing total and the posting fee are within the announced maximum price; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPostingFee(AuctionBrokerState state, uint quantity, uint unitPrice, out uint postingFee)
        => TryGetPostingFee(state.PostingFeeFlat, state.PostingFeeBasisPoints, quantity, unitPrice, state.MaximumPrice, out postingFee);

    /// <summary>
    /// Determines whether a listing price is on a price step: it is a multiple of its step
    /// (<see cref="GetPriceStep"/>). With precision 2, e.g. 350, 8,950, 590,000, 595,000 and 12,500,000 are on a
    /// step, 351, 594,000 and 12,550,000 are not.
    /// </summary>
    /// <remarks>
    /// Every listing price is restricted, the unit price of goods and the price of an item listing; purchase totals
    /// are not. The market answers <see cref="AuctionHouseResult.PriceNotOnStep"/> to a listing off a step
    /// (see <see cref="IAuctionHouseMarket.CreateListingAsync"/>) and never rounds it.
    /// </remarks>
    /// <param name="price">The unit price in Zen.</param>
    /// <param name="precision">The precision of the price steps; 0 means no restriction.</param>
    /// <returns><see langword="true"/>, if the price is a multiple of its step; otherwise, <see langword="false"/>.</returns>
    public static bool IsOnPriceStep(uint price, byte precision)
    {
        return price % GetPriceStep(price, precision) == 0;
    }

    /// <summary>
    /// Gets the price step of a price: the largest number of the form 1, 2 or 5 x 10^n which is at most
    /// floor(price / 10^precision), at least 1. With precision 2, every step is 0.4-1 % of the price, e.g. 5,000 for
    /// 595,000 and 100,000 for 12,500,000; below 200 the step is 1.
    /// </summary>
    /// <param name="price">The price in Zen.</param>
    /// <param name="precision">The precision of the price steps; 0 means no restriction, i.e. a step of 1.</param>
    /// <returns>The price step, at least 1.</returns>
    public static uint GetPriceStep(uint price, byte precision)
    {
        if (precision == 0)
        {
            return 1;
        }

        var scaled = price;
        for (var i = 0; i < precision && scaled > 0; i++)
        {
            scaled /= 10;
        }

        if (scaled == 0)
        {
            return 1;
        }

        var magnitude = 1UL;
        while (magnitude * 10 <= scaled)
        {
            magnitude *= 10;
        }

        if (magnitude * 5 <= scaled)
        {
            return (uint)(magnitude * 5);
        }

        return magnitude * 2 <= scaled ? (uint)(magnitude * 2) : (uint)magnitude;
    }

    /// <summary>
    /// Calculates the sale fee of one sale: <c>floor(gross * basisPoints / 10000)</c>, at most the gross.
    /// </summary>
    /// <param name="gross">The gross of the sale in Zen.</param>
    /// <param name="saleFeeBasisPoints">The sale fee in basis points.</param>
    /// <returns>The sale fee in Zen.</returns>
    public static uint GetSaleFee(uint gross, ushort saleFeeBasisPoints) => (uint)Math.Min(gross, GetBasisPointsOf(gross, saleFeeBasisPoints));

    /// <summary>
    /// Calculates the proceeds of one sale: the gross minus the sale fee.
    /// </summary>
    /// <param name="gross">The gross of the sale in Zen.</param>
    /// <param name="saleFeeBasisPoints">The sale fee in basis points.</param>
    /// <returns>The proceeds in Zen.</returns>
    public static uint GetProceeds(uint gross, ushort saleFeeBasisPoints) => gross - GetSaleFee(gross, saleFeeBasisPoints);
}
