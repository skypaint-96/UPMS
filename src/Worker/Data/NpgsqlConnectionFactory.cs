using System.Data;
using Npgsql;
using UPMS.Worker.Configuration;

namespace UPMS.Worker.Data;

public class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseSettings _settings;

    public NpgsqlConnectionFactory(DatabaseSettings settings)
    {
        _settings = settings;
    }

    public IDbConnection CreateConnection()
    {
        if (string.IsNullOrEmpty(_settings.Postgres))
        {
            throw new InvalidOperationException("PostgreSQL connection string is not configured");
        }

        return new NpgsqlConnection(_settings.Postgres);
    }
}
