// <copyright file="PostgreSqlTestDatabase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Persistence.EntityFramework;
using Npgsql;

/// <summary>
/// A throw-away PostgreSQL database for opt-in integration tests. The tests run only when the environment variable
/// <see cref="ServerVariableName"/> names a PostgreSQL server (<c>Host=…;Port=…;Username=…;Password=…</c>) whose user
/// may create databases and roles; otherwise they are ignored. Each instance creates its own database with a unique
/// name, applies all migrations and drops it again on disposal.
/// </summary>
internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    /// <summary>
    /// The name of the environment variable which names the PostgreSQL server.
    /// </summary>
    public const string ServerVariableName = "OPENMU_TEST_POSTGRES";

    private static readonly TestConnectionSettingProvider SettingProvider = new();

    private PostgreSqlTestDatabase(string databaseName, string connectionString)
    {
        this.DatabaseName = databaseName;
        this.ConnectionString = connectionString;
        this.ContextProvider = new PersistenceContextProvider(new NullLoggerFactory(), null);
    }

    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string of the server's user to the database, for connections beside the contexts.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Gets the persistence context provider which works on the database.
    /// </summary>
    public PersistenceContextProvider ContextProvider { get; }

    /// <summary>
    /// Creates a new database, or ignores the calling test when no server is configured.
    /// </summary>
    /// <param name="targetMigration">The migration up to which the schema is created; <see langword="null"/> for all migrations.</param>
    /// <returns>The created database.</returns>
    public static async Task<PostgreSqlTestDatabase> CreateAsync(string? targetMigration = null)
    {
        var server = Environment.GetEnvironmentVariable(ServerVariableName);
        if (string.IsNullOrWhiteSpace(server))
        {
            Assert.Ignore($"Set {ServerVariableName} to run the PostgreSQL integration tests.");
        }

        if (!ConnectionConfigurator.IsInitialized)
        {
            ConnectionConfigurator.Initialize(SettingProvider);
        }
        else if (!SettingProvider.IsUsed)
        {
            Assert.Ignore("The database connection settings were initialized by another test already.");
        }

        var databaseName = "openmu_test_" + Guid.NewGuid().ToString("N");
        SettingProvider.Use(new NpgsqlConnectionStringBuilder(server), databaseName);
        var database = new PostgreSqlTestDatabase(databaseName, new NpgsqlConnectionStringBuilder(server) { Database = databaseName }.ConnectionString);
        if (targetMigration is null)
        {
            using var update = await database.ContextProvider.ReCreateDatabaseAsync().ConfigureAwait(false);
        }
        else
        {
            await using var context = new EntityDataContext();
            await context.GetService<IMigrator>().MigrateAsync(targetMigration).ConfigureAwait(false);
        }

        return database;
    }

    /// <summary>
    /// Creates a context with the complete model, for preparing and checking data, bypassing the context abstraction.
    /// </summary>
    /// <returns>The created context.</returns>
    public EntityDataContext CreateDataContext() => new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var context = new EntityDataContext();
        await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Provides the connection settings of the current test database; the roles of the contexts are the ones of the
    /// shipped connection settings, so that the role permissions created by the migrations are used.
    /// </summary>
    private sealed class TestConnectionSettingProvider : IDatabaseConnectionSettingProvider
    {
        private NpgsqlConnectionStringBuilder? _server;
        private string? _databaseName;

        public bool IsUsed => this._server is not null;

        public Task? Initialization => null;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Use(NpgsqlConnectionStringBuilder server, string databaseName)
        {
            this._server = server;
            this._databaseName = databaseName;
        }

        public ConnectionSetting GetConnectionSetting<TContextType>()
            where TContextType : DbContext
            => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType)
        {
            var server = this._server ?? throw new InvalidOperationException("No test database in use.");
            var (user, password) = contextType.Name switch
            {
                nameof(AccountContext) or nameof(TradeContext) => ("account", "account"),
                nameof(ConfigurationContext) => ("config", "config"),
                nameof(GuildContext) => ("guild", "guild"),
                nameof(FriendContext) => ("friend", "friend"),
                _ => (server.Username, server.Password),
            };

            // The role name is parsed from "User Id=…;" (ConnectionConfigurator.GetRoleName), so this format is required.
            return new ConnectionSetting
            {
                ContextTypeName = contextType.FullName,
                ConnectionString = $"Server={server.Host};Port={server.Port};User Id={user};Password={password};Database={this._databaseName};Command Timeout=120;",
                DatabaseEngine = DatabaseEngine.Npgsql,
            };
        }
    }
}
