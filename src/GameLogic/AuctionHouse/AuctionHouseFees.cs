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
    /// Determines whether a goods unit price is on a price step: it has at most the given count of significant
    /// digits, e.g. with 3 digits 2,440,000 and 999,000 are on a step, 2,449,999 and 1,001,000 are not.
    /// </summary>
    /// <remarks>
    /// Only goods unit prices are restricted, item listings and totals are not. The auction house action can't
    /// enforce it, because only the market knows whether an inventory slot holds goods; the market answers
    /// <see cref="AuctionHouseResult.PriceNotOnStep"/> (see <see cref="IAuctionHouseMarket.CreateListingAsync"/>).
    /// </remarks>
    /// <param name="price">The unit price in Zen.</param>
    /// <param name="significantDigits">The count of significant digits which the price may have; 0 means no restriction.</param>
    /// <returns><see langword="true"/>, if the price has at most the given count of significant digits; otherwise, <see langword="false"/>.</returns>
    public static bool IsOnPriceStep(uint price, byte significantDigits)
    {
        if (significantDigits == 0)
        {
            return true;
        }

        var step = 1UL;
        for (var digits = (ulong)price; digits >= Pow10(significantDigits); digits /= 10)
        {
            step *= 10;
        }

        return price % step == 0;

        static ulong Pow10(byte exponent)
        {
            var result = 1UL;
            for (var i = 0; i < exponent && result <= uint.MaxValue; i++)
            {
                result *= 10;
            }

            return result;
        }
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
