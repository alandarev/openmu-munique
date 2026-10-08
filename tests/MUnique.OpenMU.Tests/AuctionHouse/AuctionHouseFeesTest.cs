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
    /// 2 significant digits of every listing price.
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
        Assert.That(rules.PriceSignificantDigits, Is.EqualTo(2));
        Assert.That(rules.MaximumRequestsPerSecond, Is.EqualTo(5));
        Assert.That(AuctionBrokerState.Create(rules, int.MaxValue, AuctionSellerSummary.None).PriceSignificantDigits, Is.EqualTo(2));
    }

    /// <summary>
    /// A listing price is on a price step when it has at most the given count of significant digits.
    /// </summary>
    /// <param name="price">The unit price.</param>
    /// <param name="digits">The count of significant digits.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(2_440_000u, (byte)3, true)]
    [TestCase(2_449_999u, (byte)3, false)]
    [TestCase(999_000u, (byte)3, true)]
    [TestCase(999_999u, (byte)3, false)]
    [TestCase(1_000_000u, (byte)3, true)]
    [TestCase(1_010_000u, (byte)3, true)]
    [TestCase(1_001_000u, (byte)3, false)]
    [TestCase(1u, (byte)3, true)]
    [TestCase(999u, (byte)3, true)]
    [TestCase(1_234u, (byte)3, false)]
    [TestCase(1_230u, (byte)3, true)]
    [TestCase(1_234u, (byte)1, false)]
    [TestCase(2_000_000_000u, (byte)1, true)]
    [TestCase(2_147_483_647u, (byte)3, false)]
    [TestCase(2_140_000_000u, (byte)3, true)]
    [TestCase(2_147_483_647u, (byte)10, true)]
    [TestCase(4_294_967_295u, (byte)10, true)]
    [TestCase(4_294_967_295u, (byte)9, false)]
    [TestCase(4_290_000_000u, (byte)3, true)]
    [TestCase(4_294_967_295u, (byte)255, true)]
    [TestCase(595_000u, (byte)2, false)]
    [TestCase(590_000u, (byte)2, true)]
    [TestCase(600_000u, (byte)2, true)]
    [TestCase(12_500_000u, (byte)2, false)]
    [TestCase(12_000_000u, (byte)2, true)]
    [TestCase(29_932_843u, (byte)2, false)]
    [TestCase(350u, (byte)2, true)]
    [TestCase(2_300u, (byte)2, true)]
    [TestCase(95u, (byte)2, true)]
    [TestCase(7u, (byte)2, true)]
    [TestCase(2_000_000_000u, (byte)2, true)]
    [TestCase(2_449_999u, (byte)0, true)]
    [TestCase(4_294_967_295u, (byte)0, true)]
    public void PriceStep(uint price, byte digits, bool expected)
    {
        Assert.That(AuctionHouseFees.IsOnPriceStep(price, digits), Is.EqualTo(expected));
    }
}
