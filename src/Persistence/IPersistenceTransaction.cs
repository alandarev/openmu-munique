// <copyright file="IPersistenceTransaction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Threading;

/// <summary>
/// A database transaction of a context, given to the operation of <see cref="IPlayerContext.RunInTransactionAsync{T}"/>.
/// </summary>
public interface IPersistenceTransaction
{
    /// <summary>
    /// Locks the rows of the entities with the given identifiers against changes of other transactions until this
    /// transaction ends, and loads the entities with their aggregate members into the context.
    /// </summary>
    /// <remarks>
    /// The rows are locked in the order of their identifiers, so that transactions which lock overlapping sets can't
    /// deadlock. A transaction which wants to lock a row which another transaction locked waits until that one ends,
    /// and then gets the committed state. Entities of this context with one of the identifiers are detached before,
    /// so the result never holds stale values. Only lock entities which aren't part of the graph of the context's
    /// account (e.g. market rows), because they get detached again when the transaction ends.
    /// </remarks>
    /// <typeparam name="T">The type of the entities.</typeparam>
    /// <param name="ids">The identifiers of the entities.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked entities, ordered by their identifier; identifiers without a row are left out.</returns>
    ValueTask<IReadOnlyList<T>> LockAsync<T>(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Registers an action which restores an in-memory change of the operation when the transaction fails.
    /// The actions run in the reverse order of their registration, after the database transaction got rolled back.
    /// </summary>
    /// <param name="restore">The action which restores the change.</param>
    void OnRollback(Action restore);
}
