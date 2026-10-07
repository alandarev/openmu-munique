// <copyright file="AuctionListingIndexTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Tests for the monotonic publication of the <see cref="AuctionListingIndex"/>.
/// </summary>
[TestFixture]
public class AuctionListingIndexTest
{
    private readonly Guid _id = Guid.NewGuid();

    /// <summary>
    /// Snapshots which arrive out of order can't raise the remaining quantity.
    /// </summary>
    [Test]
    public void OlderSnapshotCantRaiseRemainingQuantity()
    {
        var index = new AuctionListingIndex();
        index.Apply(this.Snapshot(AuctionListingState.Active, 10));
        index.Apply(this.Snapshot(AuctionListingState.Active, 5));
        index.Apply(this.Snapshot(AuctionListingState.Active, 8));

        Assert.That(index.Get(this._id)?.RemainingQuantity, Is.EqualTo(5));
    }

    /// <summary>
    /// A listing which left the active state stays removed, also when an older active snapshot arrives later.
    /// </summary>
    /// <param name="state">The terminal state.</param>
    [TestCase(AuctionListingState.SoldOut)]
    [TestCase(AuctionListingState.Cancelled)]
    [TestCase(AuctionListingState.Expired)]
    public void TerminalStateSticks(AuctionListingState state)
    {
        var index = new AuctionListingIndex();
        index.Apply(this.Snapshot(AuctionListingState.Active, 10));
        index.Apply(this.Snapshot(state, 0));
        index.Apply(this.Snapshot(AuctionListingState.Active, 8));

        Assert.That(index.Get(this._id), Is.Null);
        Assert.That(index.Count, Is.Zero);
    }

    /// <summary>
    /// A terminal snapshot which arrives before the creation's snapshot keeps the listing out of the index.
    /// </summary>
    [Test]
    public void TerminalStateBeforeCreationKeepsListingOut()
    {
        var index = new AuctionListingIndex();
        index.Apply(this.Snapshot(AuctionListingState.SoldOut, 0));
        index.Apply(this.Snapshot(AuctionListingState.Active, 10));

        Assert.That(index.Get(this._id), Is.Null);
    }

    /// <summary>
    /// A reset from the database replaces everything, including the removed ids.
    /// </summary>
    [Test]
    public void ResetClearsRemovedListings()
    {
        var index = new AuctionListingIndex();
        index.Apply(this.Snapshot(AuctionListingState.Cancelled, 0));
        index.Reset([AuctionIndexedListing.Of(this.Snapshot(AuctionListingState.Active, 3))]);

        Assert.That(index.Get(this._id)?.RemainingQuantity, Is.EqualTo(3));
    }

    private AuctionListing Snapshot(AuctionListingState state, int remaining)
    {
        return new Persistence.BasicModel.AuctionListing
        {
            Id = this._id,
            State = state,
            Kind = AuctionHouseListingKind.Goods,
            ItemGroup = 14,
            ItemNumber = 13,
            UnitPrice = 1_000,
            ListedQuantity = 10,
            RemainingQuantity = remaining,
        };
    }
}
