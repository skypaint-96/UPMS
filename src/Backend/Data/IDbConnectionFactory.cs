using System.Data;

namespace UPMS.Api.Data;

/// <summary>
/// Factory for creating database connections.
/// Abstracts connection creation for testability and flexibility.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new database connection.
    /// The caller is responsible for disposing the connection.
    /// </summary>
    /// <returns>A new database connection.</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// Creates and opens a new database connection asynchronously.
    /// The caller is responsible for disposing the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open database connection.</returns>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
