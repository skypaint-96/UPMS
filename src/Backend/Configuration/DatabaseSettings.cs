namespace UPMS.Api.Configuration;

/// <summary>
/// Database connection settings.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// PostgreSQL connection string.
    /// Can be overridden by DATABASE_URL environment variable.
    /// </summary>
    public string Postgres { get; set; } = string.Empty;
}
