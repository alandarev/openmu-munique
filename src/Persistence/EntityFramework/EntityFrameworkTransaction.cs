// <copyright file="EntityFrameworkTransaction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Reflection;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.EntityFramework.Json;

/// <summary>
/// The transaction of <see cref="EntityFrameworkContextBase.RunInTransactionAsync{T}"/>.
/// </summary>
internal sealed class EntityFrameworkTransaction : IPersistenceTransaction
{
    private static readonly MethodInfo LoadObjectMethod = typeof(EntityFrameworkTransaction).GetMethod(nameof(LoadObjectAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly EntityFrameworkContextBase _context;
    private readonly List<object> _lockedEntities = [];
    private readonly List<Action> _restoreActions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkTransaction"/> class.
    /// </summary>
    /// <param name="context">The context which runs the transaction.</param>
    public EntityFrameworkTransaction(EntityFrameworkContextBase context)
    {
        this._context = context;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<T>> LockAsync<T>(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
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
        var sql = $$"""SELECT "Id" AS "Value" FROM "{{entityType.GetSchema()}}"."{{entityType.GetTableName()}}" WHERE "Id" = ANY({0}) ORDER BY "Id" FOR UPDATE""";
        var lockedIds = await dbContext.Database.SqlQueryRaw<Guid>(sql, new object[] { idArray }).ToListAsync(cancellationToken).ConfigureAwait(false);

        using var contextUsage = this._context.RepositoryProvider.ContextStack.UseContext(this._context);
        (this._context.RepositoryProvider as ICacheAwareRepositoryProvider)?.EnsureCachesForCurrentGameConfiguration();
        var loadObject = LoadObjectMethod.MakeGenericMethod(entityType.ClrType);
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

            // Like accounts, the aggregate is loaded with one JSON query; references to configuration objects are
            // resolved from the cache of the current game configuration.
            var loading = (ValueTask<object?>)loadObject.Invoke(this, [id, cancellationToken])!;
            var entity = await loading.ConfigureAwait(false) as T
                         ?? throw new InvalidOperationException($"The locked {typeof(T).Name} {id} could not be loaded.");
            dbContext.Attach(entity);
            this._lockedEntities.Add(entity);
            result.Add(entity);
        }

        return result;
    }

    /// <inheritdoc />
    public void OnRollback(Action restore)
    {
        this._restoreActions.Add(restore);
    }

    /// <summary>
    /// Runs the registered restore actions in reverse order.
    /// </summary>
    /// <param name="logger">The logger for failing restore actions.</param>
    internal void Restore(ILogger logger)
    {
        for (var i = this._restoreActions.Count - 1; i >= 0; i--)
        {
            try
            {
                this._restoreActions[i]();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while restoring an in-memory change after a failed transaction.");
            }
        }
    }

    /// <summary>
    /// Detaches the locked entities with their aggregate members from the context.
    /// </summary>
    internal void DetachLockedEntities()
    {
        foreach (var entity in this._lockedEntities)
        {
            this._context.Detach(entity);
        }
    }

    /// <summary>
    /// Loads the aggregate with the given identifier through the connection of the transaction.
    /// </summary>
    /// <typeparam name="TModel">The type of the persistent model.</typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded object, if it exists.</returns>
    private async ValueTask<object?> LoadObjectAsync<TModel>(Guid id, CancellationToken cancellationToken)
        where TModel : class, IIdentifiable
    {
        return await new AccountJsonObjectLoader().LoadObjectAsync<TModel>(id, this._context.Context, cancellationToken).ConfigureAwait(false);
    }
}
