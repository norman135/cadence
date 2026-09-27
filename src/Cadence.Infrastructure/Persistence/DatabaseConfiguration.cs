using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Cadence.Infrastructure.Persistence;

/// <summary>Builds the PostgreSQL data source and DbContext options shared by the API and the migrator.</summary>
internal static class DatabaseConfiguration
{
    /// <summary>
    /// Default connection pool cap. PostgreSQL is tuned with <c>max_connections = 25</c> for small hosts,
    /// so the app keeps headroom for the migrator and admin sessions.
    /// </summary>
    internal const int DefaultMaxPoolSize = 20;

    internal const string MigrationsHistoryTable = "__ef_migrations_history";

    private static readonly string[] s_maxPoolSizeKeys = ["Maximum Pool Size", "Max Pool Size", "MaxPoolSize"];
    private static readonly string[] s_applicationNameKeys = ["Application Name", "ApplicationName"];
    private static readonly string[] s_gssEncryptionKeys = ["Gss Encryption Mode", "GssEncryptionMode"];

    /// <summary>
    /// Applies Cadence defaults to a connection string without overriding values set explicitly:
    /// a small pool size, an application name so sessions are identifiable in <c>pg_stat_activity</c>,
    /// and no GSS encryption.
    /// </summary>
    internal static string ApplyDefaults(string connectionString)
    {
        // Npgsql's builder reports every known keyword as present, so inspect the raw string instead.
        var raw = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!s_maxPoolSizeKeys.Any(raw.ContainsKey))
        {
            builder.MaxPoolSize = DefaultMaxPoolSize;
        }

        if (!s_applicationNameKeys.Any(raw.ContainsKey))
        {
            builder.ApplicationName = "cadence";
        }

        // Npgsql tries GSS (Kerberos) encryption by default, which means loading libgssapi_krb5 on
        // every new physical connection. The chiseled runtime image doesn't ship it, so each attempt
        // fails, logs an error to stderr and falls back to TLS or plain TCP. Cadence talks to its own
        // PostgreSQL, which never uses Kerberos.
        if (!s_gssEncryptionKeys.Any(raw.ContainsKey))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    internal static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        NpgsqlDataSource dataSource,
        IEnumerable<IInterceptor> interceptors) =>
        options
            .UseNpgsql(dataSource, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptors);
}
