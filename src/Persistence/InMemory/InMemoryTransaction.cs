// <copyright file="InMemoryTransaction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.InMemory;

using System.Threading;

/// <summary>
/// The transaction of <see cref="PlayerInMemoryContext.RunInTransactionAsync{T}"/>. All transactions of a provider are
/// serialized, so locking only loads the entities; a failure only runs the restore actions.
/// </summary>
internal sealed class InMemoryTransaction : IPersistenceTransaction
{
    private readonly IContext _context;
    private readonly List<Action> _restoreActions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTransaction"/> class.
    /// </summary>
    /// <param name="context">The context which runs the transaction.</param>
    public InMemoryTransaction(IContext context)
    {
        this._context = context;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<T>> LockAsync<T>(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        where T : class
    {
        var result = new List<T>();
        foreach (var id in ids.Distinct().Order())
        {
            if (await this._context.GetByIdAsync<T>(id, cancellationToken).ConfigureAwait(false) is { } entity)
            {
                result.Add(entity);
            }
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
    internal void Restore()
    {
        for (var i = this._restoreActions.Count - 1; i >= 0; i--)
        {
            this._restoreActions[i]();
        }
    }
}
