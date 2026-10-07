// <copyright file="EntityFrameworkContextBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Collections;
using System.Reflection;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using Nito.AsyncEx;
using Nito.Disposables;

/// <summary>
/// Abstract base class for an <see cref="IContext"/> which uses an <see cref="DbContext"/>.
/// </summary>
internal class EntityFrameworkContextBase : IContext
{
    private static readonly MethodInfo LoadAggregateMethod = typeof(EntityFrameworkContextBase).GetMethod(nameof(LoadAggregateCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly bool _isOwner;
    private readonly IConfigurationChangeListener? _changeListener;
    private readonly AsyncLock _lock = new();

    /// <summary>
    /// Marks the asynchronous flow which runs a transaction of <see cref="RunInTransactionAsync{T}"/> and holds <see cref="_lock"/>.
    /// </summary>
    private readonly AsyncLocal<bool> _isTransactionFlow = new();

    private readonly ILogger _logger;
    private bool _isDisposed;

    /// <summary>
    /// A value indicating whether a transaction commit of this context failed with unknown outcome, so that the
    /// in-memory state may contradict the database; such a context refuses saves and transactions.
    /// </summary>
    private volatile bool _isOutcomeUnknown;
    private int _notificationSuspensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkContextBase" /> class.
    /// </summary>
    /// <param name="context">The db context.</param>
    /// <param name="repositoryProvider">The repository provider.</param>
    /// <param name="isOwner">If set to <c>true</c>, this instance owns the <see cref="Context" />. That means it will be disposed when this instance will be disposed.</param>
    /// <param name="changeListener">The change listener.</param>
    /// <param name="logger">The logger.</param>
    protected EntityFrameworkContextBase(DbContext context, IContextAwareRepositoryProvider repositoryProvider, bool isOwner, IConfigurationChangeListener? changeListener, ILogger logger)
    {
        this.Context = context;
        this.RepositoryProvider = repositoryProvider;
        this._isOwner = isOwner;
        this._changeListener = changeListener;
        this._logger = logger;

        // Ensure that the model is created.
        _ = context.Model;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="EntityFrameworkContextBase"/> class.
    /// </summary>
    ~EntityFrameworkContextBase() => this.Dispose(false);

    /// <inheritdoc />
    public bool HasChanges => this.Context.ChangeTracker.HasChanges();

    /// <summary>
    /// Gets the entity framework context.
    /// </summary>
    internal DbContext Context { get; }

    /// <summary>
    /// Gets or sets a fault injection for tests, which is called right before (<see langword="false"/>) and right
    /// after (<see langword="true"/>) the commit of a transaction of <see cref="RunInTransactionAsync{T}"/>.
    /// </summary>
    internal Func<bool, ValueTask>? CommitFaultInjection { get; set; }

    /// <summary>
    /// Gets the repository provider.
    /// </summary>
    protected internal IContextAwareRepositoryProvider RepositoryProvider { get; }

    /// <inheritdoc/>
    public async ValueTask<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (this._isTransactionFlow.Value)
        {
            throw new InvalidOperationException("The changes of a transaction are saved when it gets committed.");
        }

        if (this._isOutcomeUnknown)
        {
            // Its in-memory state may contradict the database, see TransactionOutcomeUnknownException.
            this._logger.LogWarning("Refused to save a context after a transaction commit with unknown outcome.");
            return false;
        }

        // A player's entities can be mutated by game logic on a flow that is not serialized against
        // this save (for example item destruction on an attacker's thread during combat). Such a
        // concurrent mutation makes change detection throw while it enumerates a tracked collection.
        // The mutation is a single, quick operation, so a bounded retry lands on a stable moment
        // instead of failing the whole save - which would otherwise leave the session unpersisted and
        // roll the player back on relog.
        const int maxAttempts = 3;
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await this.SaveChangesCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientConcurrencyConflict(ex))
            {
                this._logger.LogWarning(ex, "Transient concurrency conflict while saving (attempt {Attempt}/{MaxAttempts}); retrying.", attempt, maxAttempts);
                await Task.Delay(attempt * 10, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Runs an operation as one database transaction, see <see cref="IPlayerContext.RunInTransactionAsync{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation, after the commit.</returns>
    public async ValueTask<T> RunInTransactionAsync<T>(Func<IPersistenceTransaction, ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        if (this._isTransactionFlow.Value)
        {
            throw new InvalidOperationException("Transactions can't be nested.");
        }

        if (this._isOutcomeUnknown)
        {
            throw new InvalidOperationException("The context refuses transactions after a transaction commit with unknown outcome.");
        }

        if (this._changeListener is not null)
        {
            // The configuration change publishing works on the changes of a single save, which is not needed for transactions yet.
            throw new NotSupportedException("Transactions are not supported for contexts with a configuration change listener.");
        }

        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
        this._isTransactionFlow.Value = true;
        var transaction = new EntityFrameworkTransaction(this);
        var addedBefore = this.GetAddedEntities();
        try
        {
            await using var databaseTransaction = await this.Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var isCommitting = false;
            try
            {
                var result = await operation(transaction).ConfigureAwait(false);
                await this.Context.SaveChangesAsync(false, cancellationToken).ConfigureAwait(false);

                // From here on, a failure doesn't tell whether the database committed.
                isCommitting = true;
                await this.InjectCommitFaultAsync(false).ConfigureAwait(false);
                await databaseTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                await this.InjectCommitFaultAsync(true).ConfigureAwait(false);
                this.Context.ChangeTracker.AcceptAllChanges();
                return result;
            }
            catch (Exception ex) when (isCommitting)
            {
                this._isOutcomeUnknown = true;
                this._logger.LogError(ex, "The outcome of a transaction commit is unknown; the context refuses further saves.");
                throw new TransactionOutcomeUnknownException(ex);
            }
            catch
            {
                try
                {
                    await databaseTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The database rolls the transaction back by itself when the connection is lost.
                    this._logger.LogWarning(ex, "Error while rolling back a transaction.");
                }

                await transaction.RestoreAsync(this._logger).ConfigureAwait(false);
                foreach (var added in this.GetAddedEntities().Where(entity => !addedBefore.Contains(entity)))
                {
                    this.Context.Entry(added).State = EntityState.Detached;
                }

                this.UnmarkRestoredProperties();

                throw;
            }
        }
        finally
        {
            transaction.DetachLockedEntities();
            this._isTransactionFlow.Value = false;
        }
    }

    /// <inheritdoc />
    public IDisposable SuspendChangeNotifications()
    {
        Interlocked.Increment(ref this._notificationSuspensions);
        return new Disposable(() => Interlocked.Decrement(ref this._notificationSuspensions));
    }

    /// <inheritdoc />
    public bool Detach(object item)
    {
        using var l = this.AcquireLock();
        return this.DetachInternal(item);
    }

    /// <inheritdoc />
    public void Attach(object item)
    {
        using var l = this.AcquireLock();
        this.Context.Attach(item);
    }

    /// <inheritdoc />
    public T CreateNew<T>(params object?[] args)
        where T : class
    {
        using var l = this.AcquireLock();
        var instance = typeof(CachingEntityFrameworkContext).Assembly.CreateNew<T>(args);
        this.Context.Add(instance);
        return instance;
    }

    /// <inheritdoc />
    public object CreateNew(Type type, params object?[] args)
    {
        using var l = this.AcquireLock();
        var instance = typeof(CachingEntityFrameworkContext).Assembly.CreateNew(type, args);
        this.Context.Add(instance);
        return instance;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync<T>(T obj)
        where T : class
    {
        using var l = await this.AcquireLockAsync().ConfigureAwait(false);

        var result = false;
        var entry = this.Context.Entry(obj);
        if (entry.State == EntityState.Detached)
        {
            this.Context.Attach(obj);
            entry = this.Context.Entry(obj);
        }

        switch (entry.State)
        {
            case EntityState.Detached:
                return true;
            case EntityState.Added:
                this.DetachInternal(obj);
                break;
            default:
                this.Context.Remove(obj);
                this.ForEachAggregate(obj, a => this.Context.Remove(a));
                break;
        }

        result = true;

        return result;
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken)
        where T : class
    {
        using var l = await this.AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository<T>().GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<object?> GetByIdAsync(Guid id, Type type, CancellationToken cancellationToken)
    {
        using var l = await this.AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository(type).GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<T>> GetAsync<T>(CancellationToken cancellationToken)
        where T : class
    {
        using var l = await this.AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository<T>().GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable> GetAsync(Type type, CancellationToken cancellationToken)
    {
        using var l = await this.AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        return await this.GetRepository(type).GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool IsSupporting(Type type)
    {
        var currentSearchType = type;
        do
        {
            if (currentSearchType is null)
            {
                break;
            }

            if (this.Context.Model.FindLeastDerivedEntityTypes(currentSearchType).FirstOrDefault() is not null)
            {
                return true;
            }

            if (currentSearchType.Name != currentSearchType.BaseType?.Name)
            {
                break;
            }

            currentSearchType = currentSearchType.BaseType;
        }
        while (currentSearchType != typeof(object));

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this._isDisposed)
        {
            this.Dispose(true);
        }

        this._isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Determines whether changes of an entity type are published as configuration changes.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns><see langword="true"/> when the entity belongs to the configuration schema.</returns>
    internal static bool PublishesConfigurationChanges(IReadOnlyEntityType entityType)
        => entityType.GetSchema() == SchemaNames.Configuration;

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool dispose)
    {
        if (!dispose || !this._isOwner)
        {
            return;
        }

        try
        {
            this.Context.Dispose();
        }
        catch (Exception ex)
        {
            // Disposal must never throw - e.g. Npgsql throws InvalidOperationException when the
            // underlying connection is still opening ("Can't close, connection is in state
            // Connecting") while it is being closed during a shutdown race. The context is
            // unusable afterwards either way, so logging is all there is to do.
            this._logger.LogWarning(ex, "Error while disposing the entity framework context.");
        }
    }

    /// <summary>
    /// Determines whether the exception is a transient conflict caused by a concurrent entity mutation
    /// racing this save, and is therefore worth retrying.
    /// </summary>
    /// <param name="exception">The exception thrown by the save.</param>
    /// <returns><c>true</c> if the save should be retried.</returns>
    internal static bool IsTransientConcurrencyConflict(Exception exception)
    {
        // A disposed context instance is not transient - retrying it can never succeed (e.g. a
        // save racing the disposal of its own context during a server shutdown). Note that
        // ObjectDisposedException derives from InvalidOperationException, so it has to be
        // excluded explicitly before the checks below.
        if (exception is ObjectDisposedException)
        {
            return false;
        }

        // A concurrent entity mutation racing this save corrupts the change tracker mid-enumeration.
        // Depending on exactly where change detection was, it surfaces as one of several types - a
        // modified collection (InvalidOperationException), a transiently-null internal key
        // (ArgumentNullException/NullReferenceException), or an out-of-range index. All are transient:
        // the racing mutation is a single quick operation, so a bounded retry lands on a stable moment.
        // A genuinely persistent error of the same type is not masked - it rethrows once the retries
        // are exhausted. The deterministic serialization (per-player persistence lock) is the primary
        // guard; this retry only needs to absorb the rare, bursty sources that lock isn't held for.
        return exception is DbUpdateConcurrencyException
            or InvalidOperationException
            or ArgumentNullException
            or NullReferenceException
            or IndexOutOfRangeException
            or KeyNotFoundException;
    }

    /// <summary>
    /// Loads an aggregate with one JSON query, like accounts, without tracking it. References to configuration
    /// objects are resolved from the cache of the current game configuration.
    /// </summary>
    /// <param name="modelType">The type of the persistent model of the aggregate root.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded aggregate, if it exists.</returns>
    internal async ValueTask<object?> LoadAggregateAsync(Type modelType, Guid id, CancellationToken cancellationToken)
    {
        using var contextUsage = this.RepositoryProvider.ContextStack.UseContext(this);
        (this.RepositoryProvider as ICacheAwareRepositoryProvider)?.EnsureCachesForCurrentGameConfiguration();
        var loading = (ValueTask<object?>)LoadAggregateMethod.MakeGenericMethod(modelType).Invoke(this, [id, cancellationToken])!;
        return await loading.ConfigureAwait(false);
    }

    private async ValueTask<bool> SaveChangesCoreAsync(CancellationToken cancellationToken)
    {
        using var l = await this.AcquireLockAsync().ConfigureAwait(false);

        // when we have a change publisher attached, we want to get the changed entries before accepting them.
        // Otherwise, we can accept them.
        var acceptChanges = true;

        object? sender = null;
        SavedChangesEventArgs? args = null;
        if (this._changeListener is { })
        {
            this.Context.SavedChanges += OnSavedChanges;
            acceptChanges = false;
        }

        try
        {
            await this.Context.SaveChangesAsync(acceptChanges, cancellationToken).ConfigureAwait(false);

            if (args is not null)
            {
                await this.OnSavedChangesAsync(sender, args).ConfigureAwait(false);
            }
        }
        finally
        {
            this.Context.SavedChanges -= OnSavedChanges;
        }

        return true;

        void OnSavedChanges(object? s, SavedChangesEventArgs e)
        {
            sender = s;
            args = e;
        }
    }

    /// <summary>
    /// Marks properties which the change detection of a failed save marked as modified, and which got restored to
    /// their original value since, as unmodified again; entities without modified properties become unchanged.
    /// </summary>
    private void UnmarkRestoredProperties()
    {
        this.Context.ChangeTracker.DetectChanges();
        foreach (var entry in this.Context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Modified).ToList())
        {
            foreach (var property in entry.Properties.Where(property => property.IsModified && Equals(property.CurrentValue, property.OriginalValue)))
            {
                property.IsModified = false;
            }

            if (!entry.Properties.Any(property => property.IsModified))
            {
                entry.State = EntityState.Unchanged;
            }
        }
    }

    private async ValueTask<object?> LoadAggregateCoreAsync<TModel>(Guid id, CancellationToken cancellationToken)
        where TModel : class, IIdentifiable
    {
        return await new AccountJsonObjectLoader().LoadObjectAsync<TModel>(id, this.Context, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask InjectCommitFaultAsync(bool isCommitted)
        => this.CommitFaultInjection?.Invoke(isCommitted) ?? ValueTask.CompletedTask;

    private HashSet<object> GetAddedEntities()
    {
        return this.Context.ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);
    }

    private IDisposable AcquireLock()
    {
        return this._isTransactionFlow.Value ? NoopDisposable.Instance : this._lock.Lock();
    }

    private async ValueTask<IDisposable> AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        return this._isTransactionFlow.Value ? NoopDisposable.Instance : await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool DetachInternal(object item)
    {
        var entry = this.Context.Entry(item);
        if (entry is null)
        {
            return false;
        }

        var previousState = entry.State;
        entry.State = EntityState.Detached;
        this.ForEachAggregate(item, obj => this.DetachInternal(obj));

        return previousState != EntityState.Added;
    }

    private IRepository<T> GetRepository<T>()
        where T : class
    {
        if (this.RepositoryProvider.GetRepository<T>() is { } repository)
        {
            return repository;
        }

        throw new RepositoryNotFoundException(typeof(T));
    }

    private IRepository GetRepository(Type type)
    {
        if (this.RepositoryProvider.GetRepository(type) is { } repository)
        {
            return repository;
        }

        throw new RepositoryNotFoundException(type);
    }

    private void ForEachAggregate(object obj, Action<object> action)
    {
        var aggregateProperties = obj.GetType()
            .GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<MemberOfAggregateAttribute>() is { }
                        || p.Name.StartsWith("Joined"));
        foreach (var propertyInfo in aggregateProperties)
        {
            var propertyValue = propertyInfo.GetMethod?.Invoke(obj, []);
            if (propertyValue is IEnumerable enumerable)
            {
                foreach (var value in enumerable)
                {
                    action(value);
                }
            }
            else if (propertyValue is { })
            {
                action(propertyValue);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Catching all Exceptions.")]
    private async ValueTask OnSavedChangesAsync(object? sender, SavedChangesEventArgs e)
    {
        try
        {
            if (this._changeListener is null || this._notificationSuspensions > 0)
            {
                // should never be the case
                return;
            }

            if (e.EntitiesSavedCount == 0)
            {
                // why are we getting this event then anyway?
                return;
            }

            var changedEntries = this.Context.ChangeTracker.Entries()
                .Where(entity => entity.State != EntityState.Unchanged
                                 && PublishesConfigurationChanges(entity.Metadata))
                .ToList();
            foreach (var entry in changedEntries)
            {
                var (parent, parentCollectionNavigation) = this.GetParentInformation(entry);

                switch (entry.State)
                {
                    case EntityState.Added:
                        await this._changeListener.ConfigurationAddedAsync(entry.Metadata.ClrType, entry.Entity.GetId(), entry.Entity, parent, parentCollectionNavigation).ConfigureAwait(false);
                        break;
                    case EntityState.Deleted:
                        await this._changeListener.ConfigurationRemovedAsync(entry.Metadata.ClrType, entry.Entity.GetId(), parent, parentCollectionNavigation).ConfigureAwait(false);
                        break;
                    case EntityState.Modified:
                        await this._changeListener.ConfigurationChangedAsync(entry.Metadata.ClrType, entry.Entity.GetId(), entry.Entity, parent).ConfigureAwait(false);
                        break;
                    default:
                        // no change publishing required.
                        break;
                }

                if (parent is not null && parent is not GameConfiguration && parent is not Guid)
                {
                    await this._changeListener.ConfigurationChangedAsync(parent.GetType(), parent.GetId(), parent, null).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unexpected error publishing changes.");
        }
        finally
        {
            try
            {
                this.Context.ChangeTracker.AcceptAllChanges();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Unexpected error when accepting all saved changes.");
            }
        }
    }

    private (object? Parent, INavigationBase? ParentCollectionNavigation) GetParentInformation(EntityEntry entry)
    {
        var propertyToParent = entry.Properties
            .FirstOrDefault(p => p.Metadata.IsForeignKey()
                                 && (p.Metadata.IsShadowProperty() || p.Metadata.PropertyInfo?.GetCustomAttribute<IsLinkToParentAttribute>() is not null));

        var parentId = (Guid?)(propertyToParent?.CurrentValue ?? propertyToParent?.OriginalValue);
        if (parentId is null)
        {
            return (null, null);
        }

        object? parent = null;
        INavigationBase? parentCollectionNavigation = null;
        var parentEntry = this.Context.ChangeTracker.Entries().FirstOrDefault(e => e.Entity.GetId() == parentId);
        if (parentEntry is not null && propertyToParent is not null)
        {
            parent = parentEntry.Entity;
            var parentCollection = parentEntry.Collections
                .FirstOrDefault(c => c.Metadata.IsCollection
                                     && (c.Metadata as INavigation)?.ForeignKey == propertyToParent.Metadata.GetContainingForeignKeys().FirstOrDefault());
            parentCollectionNavigation = parentCollection?.Metadata;
        }

        return (parent ?? parentId, parentCollectionNavigation);
    }
}
