// <copyright file="EntityFrameworkTransaction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// The transaction of <see cref="EntityFrameworkContextBase.RunInTransactionAsync{T}"/>.
/// </summary>
internal sealed class EntityFrameworkTransaction : IPersistenceTransaction
{
    private readonly EntityFrameworkContextBase _context;
    private readonly List<object> _lockedEntities = [];
    private readonly List<Func<ValueTask>> _restoreActions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkTransaction"/> class.
    /// </summary>
    /// <param name="context">The context which runs the transaction.</param>
    public EntityFrameworkTransaction(EntityFrameworkContextBase context)
    {
        this._context = context;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<T>> LockAsync<T>(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        where T : class
        => this.LockAsync<T>(ids, false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<T>> LockAvailableAsync<T>(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        where T : class
        => this.LockAsync<T>(ids, true, cancellationToken);

    /// <inheritdoc />
    public void DetachWhenCompleted(object entity)
    {
        this._lockedEntities.Add(entity);
    }

    /// <inheritdoc />
    public void OnRollback(Action restore)
    {
        this._restoreActions.Add(() =>
        {
            restore();
            return ValueTask.CompletedTask;
        });
    }

    /// <inheritdoc />
    public void OnRollback(Func<ValueTask> restore)
    {
        this._restoreActions.Add(restore);
    }

    /// <summary>
    /// Runs the registered restore actions in reverse order.
    /// </summary>
    /// <param name="logger">The logger for failing restore actions.</param>
    /// <returns>The task.</returns>
    internal async ValueTask RestoreAsync(ILogger logger)
    {
        for (var i = this._restoreActions.Count - 1; i >= 0; i--)
        {
            try
            {
                await this._restoreActions[i]().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while restoring an in-memory change after a failed transaction.");
            }
        }
    }

    /// <summary>
    /// Detaches the locked entities and those given to <see cref="DetachWhenCompleted"/> with their aggregate members from the context.
    /// </summary>
    internal void DetachLockedEntities()
    {
        foreach (var entity in this._lockedEntities)
        {
            this._context.Detach(entity);
        }
    }

    private async ValueTask<IReadOnlyList<T>> LockAsync<T>(IEnumerable<Guid> ids, bool skipLocked, CancellationToken cancellationToken)
        where T : class
    {
        var dbContext = this._context.Context;
        var entityType = dbContext.Model.FindLeastDerivedEntityTypes(typeof(T)).FirstOrDefault()
                         ?? throw new ArgumentException($"{typeof(T)} is not part of the model of the context.", nameof(T));
        var idArray = ids.Distinct().ToArray();
        if (idArray.Length == 0)
        {
            return [];
        }

        // FOR UPDATE locks the rows in the order in which they are returned, so ORDER BY gives a global lock order.
        var sql = $$"""SELECT "Id" AS "Value" FROM "{{entityType.GetSchema()}}"."{{entityType.GetTableName()}}" WHERE "Id" = ANY({0}) ORDER BY "Id" FOR UPDATE{{(skipLocked ? " SKIP LOCKED" : string.Empty)}}""";
        var lockedIds = await dbContext.Database.SqlQueryRaw<Guid>(sql, new object[] { idArray }).ToListAsync(cancellationToken).ConfigureAwait(false);

        var result = new List<T>(lockedIds.Count);
        foreach (var id in lockedIds)
        {
            var trackedEntry = dbContext.ChangeTracker.Entries()
                .FirstOrDefault(entry => entry.Entity is T && entry.Entity is IIdentifiable identifiable && identifiable.Id == id);
            if (trackedEntry is not null)
            {
                // It may hold values from before another transaction changed the row.
                this._context.Detach(trackedEntry.Entity);
            }

            var entity = await this._context.LoadAggregateAsync(entityType.ClrType, id, cancellationToken).ConfigureAwait(false) as T
                         ?? throw new InvalidOperationException($"The locked {typeof(T).Name} {id} could not be loaded.");
            dbContext.Attach(entity);
            this._lockedEntities.Add(entity);
            result.Add(entity);
        }

        return result;
    }
}
