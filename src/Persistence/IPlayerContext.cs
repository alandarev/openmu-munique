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
    /// Runs an operation which changes entities of this context as one database transaction: the operation may lock
    /// rows (<see cref="IPersistenceTransaction.LockAsync{T}"/>) and change tracked entities in memory; afterwards all
    /// changes of this context are saved and committed together.
    /// </summary>
    /// <remarks>
    /// No other operation on this context runs in between; calls of this context's methods from within the operation
    /// are allowed, except <see cref="IContext.SaveChangesAsync"/> and nested transactions.
    /// When the operation, the save or the commit fails, the transaction is rolled back, the restore actions
    /// registered with <see cref="IPersistenceTransaction.OnRollback"/> run in reverse order, entities which got added
    /// to this context during the operation are detached, and the exception is rethrown. The operation has to register
    /// a restore action for every in-memory change it makes before such a failure can happen, so that the in-memory
    /// objects match the database again.
    /// The locked entities are detached when the transaction ends.
    /// </remarks>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation, after the commit.</returns>
    ValueTask<T> RunInTransactionAsync<T>(Func<IPersistenceTransaction, ValueTask<T>> operation, CancellationToken cancellationToken = default);
}
