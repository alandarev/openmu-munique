// <copyright file="AuctionHouseFeesTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Tests for the <see cref="AuctionHouseFees"/>.
/// </summary>
[TestFixture]
public class AuctionHouseFeesTest
{
    /// <summary>
    /// The sale fee is floored per sale (spec scenario: 200 basis points of 1,250 Zen).
    /// </summary>
    [Test]
    public void SaleFeeIsFlooredPerSale()
    {
        Assert.That(AuctionHouseFees.GetSaleFee(1_250, 200), Is.EqualTo(25));
        Assert.That(AuctionHouseFees.GetProceeds(1_250, 200), Is.EqualTo(1_225));
        Assert.That(AuctionHouseFees.GetSaleFee(1_249, 200), Is.EqualTo(24));
        Assert.That(AuctionHouseFees.GetSaleFee(49, 200), Is.EqualTo(0));
    }

    /// <summary>
    /// The sale fee of the largest gross with the largest basis points doesn't overflow and is at most the gross.
    /// </summary>
    [Test]
    public void SaleFeeDoesNotOverflow()
    {
        Assert.That(AuctionHouseFees.GetSaleFee(int.MaxValue, 10_000), Is.EqualTo(int.MaxValue));
        Assert.That(AuctionHouseFees.GetSaleFee(uint.MaxValue, ushort.MaxValue), Is.EqualTo(uint.MaxValue));
        Assert.That(AuctionHouseFees.GetProceeds(int.MaxValue, 200), Is.EqualTo(2_147_483_647 - 42_949_672));
    }

    /// <summary>
    /// The posting fee is the flat part plus the floored basis points of the listing total.
    /// </summary>
    [Test]
    public void PostingFeeIsFlatPlusFlooredBasisPointsOfTotal()
    {
        Assert.That(AuctionHouseFees.TryGetPostingFee(30_000, 200, 1, 999, 2_000_000_000, out var fee), Is.True);
        Assert.That(fee, Is.EqualTo(30_019));

        Assert.That(AuctionHouseFees.TryGetPostingFee(1_000, 100, 18, 1_000_000, 2_000_000_000, out fee), Is.True);
        Assert.That(fee, Is.EqualTo(181_000));

        Assert.That(AuctionHouseFees.TryGetPostingFee(0, 0, 1, 1, 1, out fee), Is.True);
        Assert.That(fee, Is.EqualTo(0));
    }

    /// <summary>
    /// A total exactly at the maximum price is valid, one Zen above is not.
    /// </summary>
    [Test]
    public void TotalAtMaximumPriceBoundary()
    {
        Assert.That(AuctionHouseFees.TryGetTotal(1, 2_000_000_000, 2_000_000_000, out var total), Is.True);
        Assert.That(total, Is.EqualTo(2_000_000_000));
        Assert.That(AuctionHouseFees.TryGetTotal(1, 2_000_000_001, 2_000_000_000, out _), Is.False);
        Assert.That(AuctionHouseFees.TryGetTotal(2, 1_000_000_000, 2_000_000_000, out total), Is.True);
        Assert.That(total, Is.EqualTo(2_000_000_000));
    }

    /// <summary>
    /// The spec scenario: 30 goods at 100,000,000 Zen each exceed a maximum price of 2,000,000,000 Zen.
    /// </summary>
    [Test]
    public void GoodsTotalAboveMaximumIsOutOfRange()
    {
        Assert.That(AuctionHouseFees.TryGetTotal(30, 100_000_000, 2_000_000_000, out _), Is.False);
        Assert.That(AuctionHouseFees.TryGetPostingFee(30_000, 200, 30, 100_000_000, 2_000_000_000, out _), Is.False);
    }

    /// <summary>
    /// The billion-Zen listing of the spec scenario is within the limit.
    /// </summary>
    [Test]
    public void BillionZenListingIsWithinLimit()
    {
        Assert.That(AuctionHouseFees.TryGetPostingFee(30_000, 200, 1, 1_000_000_000, 2_000_000_000, out var fee), Is.True);
        Assert.That(fee, Is.EqualTo(20_030_000));
    }

    /// <summary>
    /// The largest Zen amount (<see cref="int.MaxValue"/>) is a valid total when the maximum price allows it.
    /// </summary>
    [Test]
    public void IntMaxValueBoundary()
    {
        Assert.That(AuctionHouseFees.TryGetTotal(1, int.MaxValue, int.MaxValue, out var total), Is.True);
        Assert.That(total, Is.EqualTo(int.MaxValue));
        Assert.That(AuctionHouseFees.TryGetTotal(1, (uint)int.MaxValue + 1, int.MaxValue, out _), Is.False);
        Assert.That(AuctionHouseFees.TryGetPostingFee(0, 10_000, 1, int.MaxValue, int.MaxValue, out var fee), Is.True);
        Assert.That(fee, Is.EqualTo(int.MaxValue));
        Assert.That(AuctionHouseFees.TryGetPostingFee(1, 10_000, 1, int.MaxValue, int.MaxValue, out _), Is.False);
    }

    /// <summary>
    /// A quantity times a unit price which overflows 32 bits is out of range instead of wrapping around.
    /// </summary>
    /// <param name="quantity">The quantity.</param>
    /// <param name="unitPrice">The unit price.</param>
    [TestCase(65_536u, 65_536u)]
    [TestCase(uint.MaxValue, uint.MaxValue)]
    [TestCase(3u, 1_431_655_766u)]
    public void QuantityTimesPriceDoesNotOverflow(uint quantity, uint unitPrice)
    {
        Assert.That(AuctionHouseFees.TryGetTotal(quantity, unitPrice, uint.MaxValue, out _), Is.False);
        Assert.That(AuctionHouseFees.TryGetPostingFee(0, 0, quantity, unitPrice, uint.MaxValue, out _), Is.False);
    }

    /// <summary>
    /// A zero quantity or unit price is out of range.
    /// </summary>
    [Test]
    public void ZeroQuantityOrPriceIsOutOfRange()
    {
        Assert.That(AuctionHouseFees.TryGetTotal(0, 1_000, 2_000_000_000, out _), Is.False);
        Assert.That(AuctionHouseFees.TryGetTotal(1, 0, 2_000_000_000, out _), Is.False);
    }

    /// <summary>
    /// The basis points of the largest amounts are computed with a 64-bit intermediate.
    /// </summary>
    [Test]
    public void BasisPointsUse64BitIntermediate()
    {
        Assert.That(AuctionHouseFees.GetBasisPointsOf(uint.MaxValue, ushort.MaxValue), Is.EqualTo((ulong)uint.MaxValue * ushort.MaxValue / 10_000));
        Assert.That(AuctionHouseFees.GetBasisPointsOf(int.MaxValue, 200), Is.EqualTo(42_949_672));
    }

    /// <summary>
    /// The announced maximum price is at most the inventory Zen limit, which is at most <see cref="int.MaxValue"/>.
    /// </summary>
    [Test]
    public void AnnouncedMaximumPriceIsAtMostZenLimit()
    {
        var rules = new AuctionHouseRules { MaximumPrice = uint.MaxValue };

        var state = AuctionBrokerState.Create(rules, 1_000_000, AuctionSellerSummary.None);

        Assert.That(state.ZenLimit, Is.EqualTo(1_000_000));
        Assert.That(state.MaximumPrice, Is.EqualTo(1_000_000));
        Assert.That(AuctionBrokerState.Create(rules, int.MaxValue, AuctionSellerSummary.None).MaximumPrice, Is.EqualTo(int.MaxValue));
        Assert.That(AuctionBrokerState.Create(rules, -1, AuctionSellerSummary.None).ZenLimit, Is.EqualTo(0));
    }

    /// <summary>
    /// The announced durations are the first four durations which are not 0.
    /// </summary>
    [Test]
    public void AnnouncedDurationsAreAtMostFour()
    {
        var rules = new AuctionHouseRules { DurationHours = [0, 6, 12, 24, 48, 72] };

        Assert.That(rules.GetOfferedDurations(), Is.EqualTo(new byte[] { 6, 12, 24, 48 }));
        Assert.That(AuctionHouseRules.Default.GetOfferedDurations(), Is.EqualTo(new byte[] { 48 }));
    }

    /// <summary>
    /// The default rules of the operator: no flat posting fee, 5 % posting fee, 10 % sale fee, 48 hours,
    /// price step precision 2 for every listing price.
    /// </summary>
    [Test]
    public void DefaultRules()
    {
        var rules = AuctionHouseRules.Default;

        Assert.That(rules.PageSize, Is.EqualTo(20));
        Assert.That(rules.MaximumActiveListings, Is.EqualTo(20));
        Assert.That(rules.MaximumPrice, Is.EqualTo(2_000_000_000));
        Assert.That(rules.PostingFeeFlat, Is.EqualTo(0));
        Assert.That(rules.PostingFeeBasisPoints, Is.EqualTo(500));
        Assert.That(rules.SaleFeeBasisPoints, Is.EqualTo(1_000));
        Assert.That(rules.RefundPostingFeeOnCancel, Is.False);
        Assert.That(rules.DurationHours, Is.EqualTo(new byte[] { 48 }));
        Assert.That(rules.PriceStepPrecision, Is.EqualTo(2));
        Assert.That(rules.MaximumRequestsPerSecond, Is.EqualTo(5));
        Assert.That(AuctionBrokerState.Create(rules, int.MaxValue, AuctionSellerSummary.None).PriceStepPrecision, Is.EqualTo(2));
    }

    /// <summary>
    /// A listing price is on a price step when it is a multiple of its step, the largest 1, 2 or 5 x 10^n which is
    /// at most price / 10^precision, at least 1.
    /// </summary>
    /// <param name="price">The unit price.</param>
    /// <param name="precision">The price step precision.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(7u, (byte)2, true)]
    [TestCase(150u, (byte)2, true)]
    [TestCase(199u, (byte)2, true)]
    [TestCase(200u, (byte)2, true)]
    [TestCase(350u, (byte)2, true)]
    [TestCase(8_900u, (byte)2, true)]
    [TestCase(8_950u, (byte)2, true)]
    [TestCase(590_000u, (byte)2, true)]
    [TestCase(595_000u, (byte)2, true)]
    [TestCase(600_000u, (byte)2, true)]
    [TestCase(1_000_000u, (byte)2, true)]
    [TestCase(1_010_000u, (byte)2, true)]
    [TestCase(2_000_000u, (byte)2, true)]
    [TestCase(5_000_000u, (byte)2, true)]
    [TestCase(12_500_000u, (byte)2, true)]
    [TestCase(12_600_000u, (byte)2, true)]
    [TestCase(2_000_000_000u, (byte)2, true)]
    [TestCase(351u, (byte)2, false)]
    [TestCase(594_000u, (byte)2, false)]
    [TestCase(12_550_000u, (byte)2, false)]
    [TestCase(29_932_843u, (byte)2, false)]
    [TestCase(93_240_000u, (byte)2, false)]
    [TestCase(4_294_967_295u, (byte)2, false)]
    [TestCase(2_440_000u, (byte)3, true)]
    [TestCase(2_449_999u, (byte)3, false)]
    [TestCase(594_000u, (byte)3, true)]
    [TestCase(595_500u, (byte)3, true)]
    [TestCase(595_100u, (byte)3, false)]
    [TestCase(1_234u, (byte)1, false)]
    [TestCase(2_000_000_000u, (byte)1, true)]
    [TestCase(4_294_967_295u, (byte)9, false)]
    [TestCase(4_294_967_294u, (byte)9, true)]
    [TestCase(4_294_967_295u, (byte)10, true)]
    [TestCase(4_294_967_295u, (byte)255, true)]
    [TestCase(594_000u, (byte)0, true)]
    [TestCase(29_932_843u, (byte)0, true)]
    [TestCase(4_294_967_295u, (byte)0, true)]
    public void PriceStep(uint price, byte precision, bool expected)
    {
        Assert.That(AuctionHouseFees.IsOnPriceStep(price, precision), Is.EqualTo(expected));
    }

    /// <summary>
    /// The price step of a price with the default precision.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <param name="expected">The expected step.</param>
    [TestCase(0u, 1u)]
    [TestCase(199u, 1u)]
    [TestCase(200u, 2u)]
    [TestCase(499u, 2u)]
    [TestCase(500u, 5u)]
    [TestCase(595_000u, 5_000u)]
    [TestCase(1_000_000u, 10_000u)]
    [TestCase(12_500_000u, 100_000u)]
    [TestCase(2_000_000_000u, 20_000_000u)]
    [TestCase(4_294_967_295u, 20_000_000u)]
    public void PriceStepOfPrice(uint price, uint expected)
    {
        Assert.That(AuctionHouseFees.GetPriceStep(price, 2), Is.EqualTo(expected));
    }

    /// <summary>
    /// Walking the grid of valid prices with the default precision from 1 up to 2,000,000,000: every next grid point
    /// is valid and every step is at most 1 % of the price (0.4-1 % from 100 on), the largest valid price below
    /// each grid point is the previous grid point, and there are 2,650 valid prices.
    /// </summary>
    [Test]
    public void PriceStepGridWalk()
    {
        const uint maximum = 2_000_000_000;
        const byte precision = 2;
        var count = 1;
        var previous = 1u;
        while (true)
        {
            var next = NextPriceOnStep(previous, precision);
            if (next > maximum)
            {
                break;
            }

            Assert.That(AuctionHouseFees.IsOnPriceStep((uint)next, precision), Is.True, $"{next} after {previous}");
            Assert.That(PreviousPriceOnStep((uint)next, precision), Is.EqualTo(previous), $"below {next}");
            var step = AuctionHouseFees.GetPriceStep((uint)next, precision);
            if (next >= 100)
            {
                Assert.That(step * 1_000UL, Is.InRange(next * 4, next * 10), $"step of {next}");
            }

            count++;
            previous = (uint)next;
        }

        Assert.That(previous, Is.EqualTo(maximum));
        Assert.That(count, Is.EqualTo(2_650));
    }

    /// <summary>
    /// Gets the next grid point after a valid price: the next multiple of its step.
    /// </summary>
    /// <param name="price">The valid price.</param>
    /// <param name="precision">The price step precision.</param>
    /// <returns>The next grid point, which may exceed <see cref="uint.MaxValue"/>.</returns>
    private static ulong NextPriceOnStep(uint price, byte precision)
    {
        var step = (ulong)AuctionHouseFees.GetPriceStep(price, precision);
        return ((price / step) + 1) * step;
    }

    /// <summary>
    /// Gets the largest valid price below a price: the price below, rounded down to its step.
    /// </summary>
    /// <param name="price">The price, at least 2.</param>
    /// <param name="precision">The price step precision.</param>
    /// <returns>The largest valid price below the price.</returns>
    private static uint PreviousPriceOnStep(uint price, byte precision)
    {
        var below = price - 1;
        var step = AuctionHouseFees.GetPriceStep(below, precision);
        return below / step * step;
    }
}
