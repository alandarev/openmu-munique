// <copyright file="RowLockBarrier.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Npgsql;

/// <summary>
/// Holds row locks on its own connection, so that competing transactions provably overlap: they start, block on the
/// locked rows, and <see cref="WaitForWaitersAsync"/> sees them all waiting before <see cref="ReleaseAsync"/> lets
/// them continue at once.
/// </summary>
internal sealed class RowLockBarrier : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private readonly string _databaseName;

    private RowLockBarrier(NpgsqlConnection connection, NpgsqlTransaction transaction, string databaseName)
    {
        this._connection = connection;
        this._transaction = transaction;
        this._databaseName = databaseName;
    }

    /// <summary>
    /// Locks rows of a table of the "data" schema.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="table">The table name.</param>
    /// <param name="ids">The ids of the rows.</param>
    /// <returns>The barrier, holding the locks.</returns>
    public static async Task<RowLockBarrier> HoldAsync(PostgreSqlTestDatabase database, string table, params Guid[] ids)
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""SELECT count(*) FROM (SELECT 1 FROM data."{table}" WHERE "Id" = ANY(@ids) ORDER BY "Id" FOR UPDATE) locked""", connection, transaction);
        command.Parameters.AddWithValue("ids", ids);
        var count = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
        Assert.That(count, Is.EqualTo(ids.Length), "the barrier locks every row");
        return new RowLockBarrier(connection, transaction, database.DatabaseName);
    }

    /// <summary>
    /// Waits until the given count of other sessions of the database wait for a lock.
    /// </summary>
    /// <param name="count">The count of waiting sessions.</param>
    /// <returns>The task.</returns>
    public async Task WaitForWaitersAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            // The statistics views are a snapshot per transaction, so the snapshot is cleared before every look.
            await this.ClearStatisticsSnapshotAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = @db AND wait_event_type = 'Lock'", this._connection, this._transaction);
            command.Parameters.AddWithValue("db", this._databaseName);
            var waiting = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
            if (waiting >= count)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                await this.ClearStatisticsSnapshotAsync().ConfigureAwait(false);
                await using var activity = new NpgsqlCommand("SELECT coalesce(string_agg(state || '/' || coalesce(wait_event_type, '-') || '/' || coalesce(wait_event, '-') || ': ' || left(query, 80), ' | '), '') FROM pg_stat_activity WHERE datname = @db", this._connection, this._transaction);
                activity.Parameters.AddWithValue("db", this._databaseName);
                Assert.Fail($"Only {waiting} of {count} sessions waited for the barrier's locks: {await activity.ExecuteScalarAsync().ConfigureAwait(false)}");
            }

            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases the locks.
    /// </summary>
    /// <returns>The task.</returns>
    public async Task ReleaseAsync()
    {
        await this._transaction.RollbackAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this._transaction.DisposeAsync().ConfigureAwait(false);
        await this._connection.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ClearStatisticsSnapshotAsync()
    {
        await using var command = new NpgsqlCommand("SELECT pg_stat_clear_snapshot()", this._connection, this._transaction);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
