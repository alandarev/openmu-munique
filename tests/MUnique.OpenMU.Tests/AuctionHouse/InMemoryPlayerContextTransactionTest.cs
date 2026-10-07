// <copyright file="InMemoryPlayerContextTransactionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests for <see cref="PlayerInMemoryContext.RunInTransactionAsync{T}"/>, which the game logic tests run against.
/// </summary>
[TestFixture]
public class InMemoryPlayerContextTransactionTest
{
    /// <summary>
    /// A failing operation runs the restore actions in reverse order and rethrows.
    /// </summary>
    [Test]
    public async Task FailureRunsRestoreActionsInReverseOrderAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewPlayerContext(new Persistence.BasicModel.GameConfiguration());
        var storage = context.CreateNew<ItemStorage>();
        storage.Money = 10;
        var restored = new List<int>();

        Assert.ThrowsAsync<InvalidOperationException>(async () => await context.RunInTransactionAsync<bool>(transaction =>
        {
            storage.Money = 20;
            transaction.OnRollback(() => restored.Add(1));
            transaction.OnRollback(() =>
            {
                restored.Add(2);
                storage.Money = 10;
            });
            throw new InvalidOperationException("Injected failure.");
        }).ConfigureAwait(false));

        Assert.That(storage.Money, Is.EqualTo(10));
        Assert.That(restored, Is.EqualTo(new[] { 2, 1 }));
    }

    /// <summary>
    /// A successful operation returns its result and locks entities by loading them.
    /// </summary>
    [Test]
    public async Task SuccessReturnsResultAndLockLoadsEntitiesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewPlayerContext(new Persistence.BasicModel.GameConfiguration());
        var storage = context.CreateNew<ItemStorage>();
        await context.SaveChangesAsync().ConfigureAwait(false);

        var locked = await context.RunInTransactionAsync(async transaction =>
            await transaction.LockAsync<ItemStorage>([storage.GetId(), Guid.NewGuid()]).ConfigureAwait(false)).ConfigureAwait(false);

        Assert.That(locked, Is.EqualTo(new[] { storage }));
    }

    /// <summary>
    /// Entities created in a failed transaction disappear, like in the entity framework variant.
    /// </summary>
    [Test]
    public async Task FailureRemovesCreatedEntitiesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewPlayerContext(new Persistence.BasicModel.GameConfiguration());
        Guid createdId = default;

        Assert.ThrowsAsync<InvalidOperationException>(async () => await context.RunInTransactionAsync<bool>(_ =>
        {
            var listing = context.CreateNew<AuctionListing>();
            listing.Escrow = context.CreateNew<ItemStorage>();
            createdId = listing.GetId();
            throw new InvalidOperationException("Injected failure.");
        }).ConfigureAwait(false));

        using var check = provider.CreateNewContext();
        Assert.That(await check.GetByIdAsync<AuctionListing>(createdId).ConfigureAwait(false), Is.Null);
        Assert.That(await check.GetAsync<ItemStorage>().ConfigureAwait(false), Is.Empty);
    }
}
