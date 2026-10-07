// <copyright file="PlayerContextTransactionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;

/// <summary>
/// PostgreSQL integration tests for <see cref="IPlayerContext.RunInTransactionAsync{T}"/>: an item moves between two
/// storages and money changes in one transaction while a row is locked; a failure rolls back and restores the in-memory
/// state; a second transaction which locks the same row waits for the first one.
/// Opt-in, see <see cref="PostgreSqlTestDatabase"/>.
/// </summary>
[TestFixture]
[NonParallelizable]
public class PlayerContextTransactionTest
{
    private const int InitialMoney = 1_000;
    private const int Price = 300;

    private PostgreSqlTestDatabase _database = null!;

    /// <summary>
    /// Creates the test database.
    /// </summary>
    [OneTimeSetUp]
    public async Task CreateDatabaseAsync()
    {
        this._database = await PostgreSqlTestDatabase.CreateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the test database.
    /// </summary>
    [OneTimeTearDown]
    public async Task DropDatabaseAsync()
    {
        if (this._database is not null)
        {
            await this._database.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The item moves from the inventory into the locked escrow storage and the money is reduced, both committed.
    /// </summary>
    [Test]
    public async Task CommitMovesItemAndMoneyTogetherAsync()
    {
        var (loginName, inventoryId, escrowId, itemId) = await this.CreateStoragesAsync().ConfigureAwait(false);
        using var playerContext = this.CreatePlayerContext();
        var inventory = await LoadVaultAsync(playerContext, loginName).ConfigureAwait(false);
        var item = inventory.Items.Single();

        var result = await playerContext.RunInTransactionAsync(async transaction =>
        {
            var escrow = (await transaction.LockAsync<ItemStorage>([escrowId]).ConfigureAwait(false)).Single();
            MoveItemAndPay(transaction, inventory, escrow, item);
            return escrow;
        }).ConfigureAwait(false);

        Assert.That(inventory.Money, Is.EqualTo(InitialMoney - Price));
        Assert.That(inventory.Items, Is.Empty);
        Assert.That(result.Items, Does.Contain(item));
        Assert.That(playerContext.HasChanges, Is.False);
        await this.AssertDatabaseStateAsync(inventoryId, InitialMoney - Price, itemId, escrowId, escrowId).ConfigureAwait(false);
    }

    /// <summary>
    /// The operation fails after its in-memory changes: the database stays unchanged, the in-memory state is restored.
    /// </summary>
    [Test]
    public async Task FailingOperationRollsBackAndRestoresAsync()
    {
        var (loginName, inventoryId, escrowId, itemId) = await this.CreateStoragesAsync().ConfigureAwait(false);
        using var playerContext = this.CreatePlayerContext();
        var inventory = await LoadVaultAsync(playerContext, loginName).ConfigureAwait(false);
        var item = inventory.Items.Single();

        Assert.ThrowsAsync<InvalidOperationException>(async () => await playerContext.RunInTransactionAsync<bool>(async transaction =>
        {
            var escrow = (await transaction.LockAsync<ItemStorage>([escrowId]).ConfigureAwait(false)).Single();
            MoveItemAndPay(transaction, inventory, escrow, item);
            throw new InvalidOperationException("Injected failure after the in-memory changes.");
        }).ConfigureAwait(false));

        await this.AssertRestoredAsync(playerContext, inventory, item, inventoryId, escrowId, itemId).ConfigureAwait(false);
    }

    /// <summary>
    /// The save fails inside the transaction (a new item violates a foreign key) after the move was written:
    /// the transaction is rolled back, the in-memory state restored and the new item detached.
    /// </summary>
    [Test]
    public async Task FailingSaveRollsBackAndRestoresAsync()
    {
        var (loginName, inventoryId, escrowId, itemId) = await this.CreateStoragesAsync().ConfigureAwait(false);
        using var playerContext = this.CreatePlayerContext();
        var inventory = await LoadVaultAsync(playerContext, loginName).ConfigureAwait(false);
        var item = inventory.Items.Single();

        Assert.ThrowsAsync<DbUpdateException>(async () => await playerContext.RunInTransactionAsync(async transaction =>
        {
            var escrow = (await transaction.LockAsync<ItemStorage>([escrowId]).ConfigureAwait(false)).Single();
            MoveItemAndPay(transaction, inventory, escrow, item);
            var invalidItem = playerContext.CreateNew<Item>();
            ((Persistence.EntityFramework.Model.Item)invalidItem).ItemStorageId = Guid.NewGuid();
            return true;
        }).ConfigureAwait(false));

        await this.AssertRestoredAsync(playerContext, inventory, item, inventoryId, escrowId, itemId).ConfigureAwait(false);
    }

    /// <summary>
    /// A second transaction which locks the same row waits until the first one committed, and then sees its changes.
    /// </summary>
    [Test]
    public async Task SecondTransactionWaitsForLockedRowAsync()
    {
        var (_, _, escrowId, _) = await this.CreateStoragesAsync().ConfigureAwait(false);
        using var firstContext = this.CreatePlayerContext();
        using var secondContext = this.CreatePlayerContext();
        var firstLocked = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();
        var secondLocked = false;

        var first = firstContext.RunInTransactionAsync(async transaction =>
        {
            var escrow = (await transaction.LockAsync<ItemStorage>([escrowId]).ConfigureAwait(false)).Single();
            escrow.Money += 1;
            firstLocked.SetResult();
            await releaseFirst.Task.ConfigureAwait(false);
            return escrow.Money;
        }).AsTask();
        await firstLocked.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        var second = secondContext.RunInTransactionAsync(async transaction =>
        {
            var escrow = (await transaction.LockAsync<ItemStorage>([escrowId]).ConfigureAwait(false)).Single();
            secondLocked = true;
            escrow.Money += 10;
            return escrow.Money;
        }).AsTask();

        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        Assert.That(secondLocked, Is.False, "The second transaction got the lock while the first one held it.");

        releaseFirst.SetResult();
        Assert.That(await first.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(await second.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false), Is.EqualTo(11));

        await using var dataContext = this._database.CreateDataContext();
        var money = await dataContext.Set<Persistence.EntityFramework.Model.ItemStorage>()
            .Where(storage => storage.Id == escrowId).Select(storage => storage.Money).SingleAsync().ConfigureAwait(false);
        Assert.That(money, Is.EqualTo(11));
    }

    private static void MoveItemAndPay(IPersistenceTransaction transaction, ItemStorage inventory, ItemStorage escrow, Item item)
    {
        inventory.Money -= Price;
        transaction.OnRollback(() => inventory.Money += Price);

        var slot = item.ItemSlot;
        inventory.Items.Remove(item);
        escrow.Items.Add(item);
        item.ItemSlot = 0;
        transaction.OnRollback(() =>
        {
            escrow.Items.Remove(item);
            item.ItemSlot = slot;
            inventory.Items.Add(item);
        });
    }

    private async Task AssertRestoredAsync(IPlayerContext playerContext, ItemStorage inventory, Item item, Guid inventoryId, Guid escrowId, Guid itemId)
    {
        Assert.That(inventory.Money, Is.EqualTo(InitialMoney));
        Assert.That(inventory.Items, Does.Contain(item));
        Assert.That(item.ItemSlot, Is.EqualTo(12));
        Assert.That(((Persistence.EntityFramework.Model.Item)item).ItemStorageId, Is.EqualTo(inventoryId));
        Assert.That(playerContext.HasChanges, Is.False, "The restored in-memory state differs from the tracked database state.");
        await this.AssertDatabaseStateAsync(inventoryId, InitialMoney, itemId, inventoryId, escrowId).ConfigureAwait(false);

        // The context stays usable: a later save of the player works and changes nothing.
        Assert.That(await playerContext.SaveChangesAsync().ConfigureAwait(false), Is.True);
        await this.AssertDatabaseStateAsync(inventoryId, InitialMoney, itemId, inventoryId, escrowId).ConfigureAwait(false);
    }

    private async Task AssertDatabaseStateAsync(Guid inventoryId, int expectedMoney, Guid itemId, Guid expectedStorageId, Guid escrowId)
    {
        await using var dataContext = this._database.CreateDataContext();
        var money = await dataContext.Set<Persistence.EntityFramework.Model.ItemStorage>()
            .Where(storage => storage.Id == inventoryId).Select(storage => storage.Money).SingleAsync().ConfigureAwait(false);
        var storageId = await dataContext.Set<Persistence.EntityFramework.Model.Item>()
            .Where(i => i.Id == itemId).Select(i => i.ItemStorageId).SingleAsync().ConfigureAwait(false);
        var itemCount = await dataContext.Set<Persistence.EntityFramework.Model.Item>()
            .CountAsync(i => i.ItemStorageId == inventoryId || i.ItemStorageId == escrowId).ConfigureAwait(false);
        Assert.That(money, Is.EqualTo(expectedMoney));
        Assert.That(storageId, Is.EqualTo(expectedStorageId));
        Assert.That(itemCount, Is.EqualTo(1));
    }

    private IPlayerContext CreatePlayerContext()
    {
        return this._database.ContextProvider.CreateNewPlayerContext(new Persistence.EntityFramework.Model.GameConfiguration());
    }

    private static async Task<ItemStorage> LoadVaultAsync(IPlayerContext playerContext, string loginName)
    {
        var account = await playerContext.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
        Assert.That(account?.Vault, Is.Not.Null);
        return account!.Vault!;
    }

    /// <summary>
    /// Creates an account whose vault (the storage of the player's account graph) holds the money and one item,
    /// and an empty escrow storage which belongs to no account.
    /// </summary>
    private async Task<(string LoginName, Guid InventoryId, Guid EscrowId, Guid ItemId)> CreateStoragesAsync()
    {
        using var context = this._database.ContextProvider.CreateNewContext();
        var account = context.CreateNew<Account>();
        account.LoginName = "tx" + Guid.NewGuid().ToString("N")[..8];
        account.Vault = context.CreateNew<ItemStorage>();
        var inventory = account.Vault;
        inventory.Money = InitialMoney;
        var escrow = context.CreateNew<ItemStorage>();
        var item = context.CreateNew<Item>();
        item.ItemSlot = 12;
        item.Durability = 1;
        inventory.Items.Add(item);
        Assert.That(await context.SaveChangesAsync().ConfigureAwait(false), Is.True);
        await this.AssertDatabaseStateAsync(inventory.GetId(), InitialMoney, item.GetId(), inventory.GetId(), escrow.GetId()).ConfigureAwait(false);
        return (account.LoginName, inventory.GetId(), escrow.GetId(), item.GetId());
    }
}
