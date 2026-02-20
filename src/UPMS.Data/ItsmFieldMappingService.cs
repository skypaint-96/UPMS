namespace UPMS.Data;

using System.Data;
using Dapper;
using Microsoft.Extensions.Options;

/// <summary>
/// Database-backed service for canonical field name lookups.
/// </summary>
public class ItsmFieldMappingService : IItsmFieldMappingService
{
    private readonly Func<IDbConnection> _connectionFactory;

    public ItsmFieldMappingService(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public static ItsmFieldMappingService CreateFromOptions(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var cs = options.Value.ConnectionString;
        return new ItsmFieldMappingService(() => new Npgsql.NpgsqlConnection(cs));
    }

    public string GetCanonicalName(string itsmSource, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        using var conn = _connectionFactory();
        var result = conn.QueryFirstOrDefault<string>(
            "SELECT canonical_field_name FROM itsm_field_mapping WHERE itsm_source = @ItsmSource AND source_field_name = @SourceFieldName",
            new { ItsmSource = itsmSource, SourceFieldName = sourceFieldName });
        return result ?? sourceFieldName;
    }

    public IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);

        using var conn = _connectionFactory();
        return conn.Query<ItsmFieldMapping>(
            "SELECT itsm_source AS ItsmSource, source_field_name AS SourceFieldName, canonical_field_name AS CanonicalFieldName FROM itsm_field_mapping WHERE itsm_source = @ItsmSource ORDER BY source_field_name",
            new { ItsmSource = itsmSource }).ToList();
    }

    public async Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalFieldName);

        using var conn = _connectionFactory();
        // Use INSERT OR REPLACE for SQLite compatibility; real impl uses PostgreSQL UPSERT
        string providerName = conn.GetType().FullName ?? string.Empty;
        string sql = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? "INSERT OR REPLACE INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name) VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName)"
            : "INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name) VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName) ON CONFLICT (itsm_source, source_field_name) DO UPDATE SET canonical_field_name = EXCLUDED.canonical_field_name";
        await conn.ExecuteAsync(sql, new { ItsmSource = itsmSource, SourceFieldName = sourceFieldName, CanonicalFieldName = canonicalFieldName });
    }
}
