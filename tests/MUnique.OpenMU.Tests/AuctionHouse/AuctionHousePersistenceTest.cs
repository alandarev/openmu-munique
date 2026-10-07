// <copyright file="AuctionHousePersistenceTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using EfModel = MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// PostgreSQL integration tests for the auction house entities and the migration <c>AddAuctionHouse</c>.
/// Opt-in, see <see cref="PostgreSqlTestDatabase"/>.
/// </summary>
[TestFixture]
[NonParallelizable]
public class AuctionHousePersistenceTest
{
    private const string PreviousMigration = "20260926142218_AddItemRuleFlags";

    /// <summary>
    /// The migration applies to a database which was created with the schema before it, and nothing remains pending.
    /// </summary>
    [Test]
    public async Task MigrationAppliesToPreviousSchemaAsync()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(PreviousMigration).ConfigureAwait(false);
        await using (var context = database.CreateDataContext())
        {
            Assert.That(await context.Database.GetAppliedMigrationsAsync().ConfigureAwait(false), Has.Some.EqualTo(PreviousMigration));
            Assert.That(await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false), Has.Some.EndsWith("_AddAuctionHouse"));
        }

        await database.ContextProvider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);

        Assert.That(await database.ContextProvider.IsDatabaseUpToDateAsync().ConfigureAwait(false), Is.True);
        await using (var context = database.CreateDataContext())
        {
            Assert.That(await context.Set<EfModel.AuctionListing>().CountAsync().ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.AuctionCollectionEntry>().CountAsync().ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.AuctionSale>().CountAsync().ConfigureAwait(false), Is.Zero);
        }
    }

    /// <summary>
    /// A player context locks a listing, gets it with its escrowed item, and commits a change of it.
    /// </summary>
    [Test]
    public async Task PlayerContextLocksAndChangesListingAsync()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync().ConfigureAwait(false);
        var (gameConfiguration, character) = await CreateCharacterAsync(database).ConfigureAwait(false);
        var (listingId, _, itemId) = await CreateMarketRowsAsync(database, character).ConfigureAwait(false);

        using (var playerContext = database.ContextProvider.CreateNewPlayerContext(gameConfiguration))
        {
            await playerContext.RunInTransactionAsync(async transaction =>
            {
                var listing = (await transaction.LockAsync<AuctionListing>([listingId]).ConfigureAwait(false)).Single();
                Assert.That(listing.Escrow?.Items.Select(item => item.GetId()), Is.EqualTo(new[] { itemId }));
                Assert.That(listing.Escrow!.Items.Single().Durability, Is.EqualTo(1));
                listing.RemainingQuantity = 0;
                listing.State = AuctionListingState.SoldOut;
                return true;
            }).ConfigureAwait(false);
        }

        await using var context = database.CreateDataContext();
        var stored = await context.Set<EfModel.AuctionListing>().SingleAsync(listing => listing.Id == listingId).ConfigureAwait(false);
        Assert.That(stored.State, Is.EqualTo(AuctionListingState.SoldOut));
        Assert.That(stored.RemainingQuantity, Is.Zero);
    }

    /// <summary>
    /// Deleting a character deletes its listings and collection entries with their escrow storages and items;
    /// sale records stay.
    /// </summary>
    [Test]
    public async Task DeletingCharacterDeletesItsListingsAndEntriesWithItemsAsync()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync().ConfigureAwait(false);
        var (gameConfiguration, character) = await CreateCharacterAsync(database).ConfigureAwait(false);
        var (listingId, entryId, itemId) = await CreateMarketRowsAsync(database, character).ConfigureAwait(false);
        Guid escrowIdOfListing;
        await using (var context = database.CreateDataContext())
        {
            escrowIdOfListing = (await context.Set<EfModel.AuctionListing>().SingleAsync(listing => listing.Id == listingId).ConfigureAwait(false)).EscrowId!.Value;
        }

        using (var playerContext = database.ContextProvider.CreateNewPlayerContext(gameConfiguration))
        {
            var account = await playerContext.GetAccountByLoginNameAsync(character.Name).ConfigureAwait(false);
            var loadedCharacter = account!.Characters.Single();
            account.Characters.Remove(loadedCharacter);
            Assert.That(await playerContext.DeleteAsync(loadedCharacter).ConfigureAwait(false), Is.True);
            Assert.That(await playerContext.SaveChangesAsync().ConfigureAwait(false), Is.True);
        }

        await using (var context = database.CreateDataContext())
        {
            Assert.That(await context.Set<EfModel.Character>().CountAsync(c => c.Id == character.GetId()).ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.AuctionListing>().CountAsync(listing => listing.Id == listingId).ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.AuctionCollectionEntry>().CountAsync(entry => entry.Id == entryId).ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.ItemStorage>().CountAsync(storage => storage.Id == escrowIdOfListing).ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.Item>().CountAsync(item => item.Id == itemId).ConfigureAwait(false), Is.Zero);
            Assert.That(await context.Set<EfModel.Item>().CountAsync().ConfigureAwait(false), Is.Zero, "An escrowed item remained.");
            Assert.That(await context.Set<EfModel.AuctionSale>().CountAsync(sale => sale.ListingId == listingId).ConfigureAwait(false), Is.EqualTo(1));
        }
    }

    private static async Task<(GameConfiguration GameConfiguration, Character Character)> CreateCharacterAsync(PostgreSqlTestDatabase database)
    {
        using var context = database.ContextProvider.CreateNewContext();
        var gameConfiguration = context.CreateNew<GameConfiguration>();
        var characterClass = context.CreateNew<CharacterClass>();
        gameConfiguration.CharacterClasses.Add(characterClass);
        var account = context.CreateNew<Account>();
        var character = context.CreateNew<Character>();
        character.Name = "seller" + Random.Shared.Next(1000, 9999);
        account.LoginName = character.Name;
        character.CharacterClass = characterClass;
        character.Inventory = context.CreateNew<ItemStorage>();
        account.Characters.Add(character);
        Assert.That(await context.SaveChangesAsync().ConfigureAwait(false), Is.True);
        return (gameConfiguration, character);
    }

    private static async Task<(Guid ListingId, Guid EntryId, Guid ItemId)> CreateMarketRowsAsync(PostgreSqlTestDatabase database, Character character)
    {
        using var context = database.ContextProvider.CreateNewContext();
        var listing = context.CreateNew<AuctionListing>();
        listing.SellerCharacterId = character.GetId();
        listing.SellerName = character.Name;
        listing.Kind = AuctionHouseListingKind.Item;
        listing.ItemGroup = 0;
        listing.ItemNumber = 1;
        listing.UnitPrice = 1_000;
        listing.ListedQuantity = 1;
        listing.RemainingQuantity = 1;
        listing.CreatedAt = DateTime.UtcNow;
        listing.ExpiresAt = listing.CreatedAt.AddHours(48);
        listing.State = AuctionListingState.Active;
        listing.Escrow = context.CreateNew<ItemStorage>();
        var item = context.CreateNew<Item>();
        item.Durability = 1;
        listing.Escrow.Items.Add(item);

        var entry = context.CreateNew<AuctionCollectionEntry>();
        entry.OwnerCharacterId = character.GetId();
        entry.Kind = AuctionHouseCollectionEntryKind.Purchase;
        entry.Quantity = 1;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Escrow = context.CreateNew<ItemStorage>();
        var entryItem = context.CreateNew<Item>();
        entryItem.Durability = 1;
        entry.Escrow.Items.Add(entryItem);

        var sale = context.CreateNew<AuctionSale>();
        sale.ListingId = listing.GetId();
        sale.SellerCharacterId = character.GetId();
        sale.SellerName = character.Name;
        sale.BuyerName = "buyer";
        sale.SoldAt = DateTime.UtcNow;

        Assert.That(await context.SaveChangesAsync().ConfigureAwait(false), Is.True);
        return (listing.GetId(), entry.GetId(), item.GetId());
    }
}
