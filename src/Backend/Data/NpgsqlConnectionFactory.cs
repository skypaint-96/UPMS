using System.Data;
using Npgsql;
using UPMS.Api.Configuration;

namespace UPMS.Api.Data;

/// <summary>
/// PostgreSQL implementation of IDbConnectionFactory using Npgsql.
/// </summary>
public class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Creates a new instance of NpgsqlConnectionFactory.
    /// </summary>
    /// <param name="settings">Database settings containing the connection string.</param>
    /// <exception cref="ArgumentException">Thrown when connection string is empty.</exception>
    public NpgsqlConnectionFactory(DatabaseSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Postgres))
        {
            throw new ArgumentException(
                "PostgreSQL connection string is not configured. " +
                "Set DATABASE_URL environment variable or ConnectionStrings:Postgres in appsettings.json.");
        }

        _connectionString = settings.Postgres;
    }

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    /// <inheritdoc />
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
