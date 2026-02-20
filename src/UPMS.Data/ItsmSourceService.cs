namespace UPMS.Data;

using System.Data;
using Dapper;
using Microsoft.Extensions.Options;

/// <summary>
/// Database-backed implementation of <see cref="IItsmSourceService"/>.
/// Compatible with both SQLite (tests) and PostgreSQL (production).
/// Dialect is detected from the connection provider type at runtime.
/// </summary>
public class ItsmSourceService : IItsmSourceService
{
    private readonly Func<IDbConnection> _connectionFactory;

    public ItsmSourceService(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public static ItsmSourceService CreateFromOptions(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var cs = options.Value.ConnectionString;
        return new ItsmSourceService(() => new Npgsql.NpgsqlConnection(cs));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private bool IsSqlite(IDbConnection conn) =>
        conn.GetType().FullName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;

    // ── Sources ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ItsmSource>> GetAllSourcesAsync()
    {
        using var conn = _connectionFactory();
        var rows = await conn.QueryAsync<ItsmSource>(
            "SELECT id AS Id, name AS Name, display_label AS DisplayLabel FROM itsm_source ORDER BY name");
        return rows.ToList().AsReadOnly();
    }

    public async Task<ItsmSource?> GetSourceByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var conn = _connectionFactory();
        return await conn.QueryFirstOrDefaultAsync<ItsmSource>(
            "SELECT id AS Id, name AS Name, display_label AS DisplayLabel FROM itsm_source WHERE name = @Name",
            new { Name = name });
    }

    public async Task<ItsmSourceDefinition> GetSourceDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var source = await GetSourceByNameAsync(name)
            ?? throw new KeyNotFoundException($"ITSM source '{name}' was not found.");

        var mappings = await GetMappingsForSourceInternalAsync(name);

        return new ItsmSourceDefinition
        {
            Source = source,
            Mappings = mappings
        };
    }

    public async Task<ItsmSource> CreateSourceAsync(string name, string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);

        using var conn = _connectionFactory();

        if (IsSqlite(conn))
        {
            await conn.ExecuteAsync(
                "INSERT INTO itsm_source (name, display_label) VALUES (@Name, @DisplayLabel)",
                new { Name = name, DisplayLabel = displayLabel });

            return await conn.QueryFirstAsync<ItsmSource>(
                "SELECT id AS Id, name AS Name, display_label AS DisplayLabel FROM itsm_source WHERE name = @Name",
                new { Name = name });
        }
        else
        {
            return await conn.QueryFirstAsync<ItsmSource>(
                """
                INSERT INTO itsm_source (name, display_label)
                VALUES (@Name, @DisplayLabel)
                RETURNING id AS Id, name AS Name, display_label AS DisplayLabel
                """,
                new { Name = name, DisplayLabel = displayLabel });
        }
    }

    public async Task DeleteSourceAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var conn = _connectionFactory();
        // Delete mappings first (no FK cascade defined in migration for SQLite compat)
        await conn.ExecuteAsync(
            "DELETE FROM itsm_field_mapping WHERE itsm_source = @Name",
            new { Name = name });
        await conn.ExecuteAsync(
            "DELETE FROM itsm_source WHERE name = @Name",
            new { Name = name });
    }

    // ── Mappings ───────────────────────────────────────────────────────────

    public async Task UpsertMappingAsync(string sourceName, string sourceFieldName, string canonicalName, bool isRequired)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);

        using var conn = _connectionFactory();

        string sql = IsSqlite(conn)
            ? """
              INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
              VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName, @IsRequired)
              ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                  SET canonical_field_name = excluded.canonical_field_name,
                      is_required          = excluded.is_required
              """
            : """
              INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
              VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName, @IsRequired)
              ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                  SET canonical_field_name = EXCLUDED.canonical_field_name,
                      is_required          = EXCLUDED.is_required
              """;

        await conn.ExecuteAsync(sql, new
        {
            ItsmSource = sourceName,
            SourceFieldName = sourceFieldName,
            CanonicalFieldName = canonicalName,
            IsRequired = isRequired
        });
    }

    public async Task DeleteMappingAsync(string sourceName, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        using var conn = _connectionFactory();
        await conn.ExecuteAsync(
            "DELETE FROM itsm_field_mapping WHERE itsm_source = @ItsmSource AND source_field_name = @SourceFieldName",
            new { ItsmSource = sourceName, SourceFieldName = sourceFieldName });
    }

    public async Task<IReadOnlyList<string>> GetRequiredFieldsAsync(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        using var conn = _connectionFactory();
        var rows = await conn.QueryAsync<string>(
            "SELECT source_field_name FROM itsm_field_mapping WHERE itsm_source = @ItsmSource AND is_required = @IsRequired ORDER BY source_field_name",
            new { ItsmSource = sourceName, IsRequired = true });
        return rows.ToList().AsReadOnly();
    }

    public async Task<string?> GetCanonicalNameAsync(string sourceName, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        using var conn = _connectionFactory();
        return await conn.QueryFirstOrDefaultAsync<string?>(
            "SELECT canonical_field_name FROM itsm_field_mapping WHERE itsm_source = @ItsmSource AND source_field_name = @SourceFieldName",
            new { ItsmSource = sourceName, SourceFieldName = sourceFieldName });
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ItsmFieldMapping>> GetMappingsForSourceInternalAsync(string sourceName)
    {
        using var conn = _connectionFactory();
        var rows = await conn.QueryAsync<ItsmFieldMapping>(
            """
            SELECT itsm_source        AS ItsmSource,
                   source_field_name  AS SourceFieldName,
                   canonical_field_name AS CanonicalFieldName,
                   is_required        AS IsRequired
            FROM   itsm_field_mapping
            WHERE  itsm_source = @ItsmSource
            ORDER  BY source_field_name
            """,
            new { ItsmSource = sourceName });
        return rows.ToList().AsReadOnly();
    }
}
