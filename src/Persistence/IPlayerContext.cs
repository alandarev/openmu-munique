// <copyright file="IPlayerContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Threading;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Persistence context which is used by in-game players.
/// </summary>
public interface IPlayerContext : IContext
{
    /// <summary>
    /// Gets a value indicating whether this context is quarantined: its in-memory state may contradict the database,
    /// e.g. after a <see cref="TransactionOutcomeUnknownException"/>, so it refuses every save and transaction. The
    /// owner of the context must not move anything of it anymore and has to drop it.
    /// </summary>
    bool IsQuarantined { get; }

    /// <summary>
    /// Gets the letter body by the id of its header.
    /// </summary>
    /// <param name="headerId">The id of its header.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The body of the header.
    /// </returns>
    ValueTask<LetterBody?> GetLetterBodyByHeaderIdAsync(Guid headerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the letter can be saved.
    /// </summary>
    /// <param name="letterHeader">The letter header.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///   <c>True</c>, if successful; Otherwise, <c>false</c>.
    /// </returns>
    ValueTask<bool> CanSaveLetterAsync(LetterHeader letterHeader, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates the account by login name and password, returning minimal account state.
    /// This is a lightweight alternative to <see cref="GetAccountByLoginNameAsync(string, string, CancellationToken)"/>
    /// that avoids loading the full account data.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The <see cref="AccountState"/> if credentials are valid; otherwise, null.
    /// </returns>
    ValueTask<AccountState?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the account by login name if the password is correct.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The account, if the password is correct. Otherwise, null.
    /// </returns>
    ValueTask<Account?> GetAccountByLoginNameAsync(string loginName, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the account by login name.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The account, if exists. Otherwise, null.
    /// </returns>
    ValueTask<Account?> GetAccountByLoginNameAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the accounts ordered by login name.
    /// </summary>
    /// <param name="skip">The skip count.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The account objects, without dependent data.
    /// </returns>
    ValueTask<IEnumerable<Account>> GetAccountsOrderedByLoginNameAsync(int skip, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the accounts which contain the search term in their login name or in one of
    /// their character names, ordered by login name.
    /// </summary>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="skip">The skip count.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The account objects, without dependent data.
    /// </returns>
    ValueTask<IEnumerable<Account>> SearchAccountsAsync(string searchTerm, int skip, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the account by character name.
    /// </summary>
    /// <param name="characterName">The character name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The account; Otherwise, null.
    /// </returns>
    ValueTask<Account?> GetAccountByCharacterNameAsync(string characterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending Castle Siege rewards for a character.
    /// </summary>
    /// <param name="characterId">The persistent character identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pending Castle Siege rewards.</returns>
    ValueTask<IReadOnlyList<CastleSiegePendingReward>> GetPendingCastleSiegeRewardsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active auction house listings with their escrowed items, without tracking them in this context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active listings.</returns>
    ValueTask<IReadOnlyList<AuctionListing>> GetActiveAuctionListingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active auction house listings of a character.
    /// </summary>
    /// <param name="sellerCharacterId">The identifier of the selling character.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count of active listings.</returns>
    ValueTask<int> GetActiveAuctionListingCountAsync(Guid sellerCharacterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the identifiers of the auction house collection entries of a character, oldest first (by the time of
    /// their last change, then by identifier).
    /// </summary>
    /// <param name="ownerCharacterId">The identifier of the owning character.</param>
    /// <param name="listingId">If set, only the entries which result from this listing.</param>
    /// <param name="kind">If set, only the entries of this kind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifiers of the entries.</returns>
    ValueTask<IReadOnlyList<Guid>> GetAuctionCollectionEntryIdsAsync(Guid ownerCharacterId, Guid? listingId = null, AuctionHouseCollectionEntryKind? kind = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets auction house collection entries with their items, without tracking them in this context.
    /// </summary>
    /// <param name="ids">The identifiers of the entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries which exist, in the order of the identifiers.</returns>
    ValueTask<IReadOnlyList<AuctionCollectionEntry>> GetAuctionCollectionEntriesAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets auction house listings in any state with their escrowed items, without tracking them in this context.
    /// </summary>
    /// <param name="ids">The identifiers of the listings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listings which exist, in the order of the identifiers.</returns>
    ValueTask<IReadOnlyList<AuctionListing>> GetAuctionListingsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the identifiers of the active auction house goods listings of an item type and level which aren't expired,
    /// aren't of the excluded seller and don't cost more than a unit price, ordered by identifier.
    /// </summary>
    /// <param name="itemGroup">The item group.</param>
    /// <param name="itemNumber">The item number.</param>
    /// <param name="itemLevel">The item level.</param>
    /// <param name="excludedSellerCharacterId">The identifier of the seller whose listings are left out.</param>
    /// <param name="maximumUnitPrice">The maximum unit price.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifiers of the listings.</returns>
    ValueTask<IReadOnlyList<Guid>> GetActiveGoodsListingIdsAsync(byte itemGroup, short itemNumber, byte itemLevel, Guid excludedSellerCharacterId, long maximumUnitPrice, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the active auction house listings whose duration ended, oldest expiry first, in one statement which skips
    /// the rows that other transactions have locked (<c>FOR UPDATE SKIP LOCKED</c> with a limit), so that locked rows
    /// never hide the due rows behind them; the listings are loaded like by <see cref="IPersistenceTransaction.LockAsync{T}"/>.
    /// </summary>
    /// <param name="transaction">The transaction of <see cref="RunInTransactionAsync{T}"/> of this context.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="maximumCount">The maximum count of listings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked due listings.</returns>
    ValueTask<IReadOnlyList<AuctionListing>> LockDueAuctionListingsAsync(IPersistenceTransaction transaction, DateTime now, int maximumCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quarantines this context, see <see cref="IsQuarantined"/>. A transaction whose commit outcome is unknown does it
    /// by itself.
    /// </summary>
    void Quarantine();

    /// <summary>
    /// Runs an operation which changes entities of this context as one database transaction: the operation may lock
    /// rows (<see cref="IPersistenceTransaction.LockAsync{T}"/>) and change tracked entities in memory; afterwards all
    /// changes of this context are saved and committed together.
    /// </summary>
    /// <remarks>
    /// No other operation on this context runs in between; calls of this context's methods from within the operation
    /// are allowed, except <see cref="IContext.SaveChangesAsync"/> and nested transactions.
    /// When the operation, the save or the commit fails, the transaction is rolled back, the restore actions
    /// registered with <see cref="IPersistenceTransaction.OnRollback(Action)"/> run in reverse order, entities which got added
    /// to this context during the operation are detached, and the exception is rethrown. The operation has to register
    /// a restore action for every in-memory change it makes before such a failure can happen, so that the in-memory
    /// objects match the database again.
    /// The locked entities are detached when the transaction ends.
    /// A failure in or after the commit call doesn't tell whether the database committed: then no restore action runs,
    /// a <see cref="TransactionOutcomeUnknownException"/> is thrown, and the context refuses every further save and
    /// transaction, so that its in-memory state is never written over the durable state.
    /// </remarks>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation, after the commit.</returns>
    ValueTask<T> RunInTransactionAsync<T>(Func<IPersistenceTransaction, ValueTask<T>> operation, CancellationToken cancellationToken = default);
}
