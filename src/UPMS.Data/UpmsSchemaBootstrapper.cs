namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

public class UpmsSchemaBootstrapper
{
    private readonly UpmsDbContext _context;

    public UpmsSchemaBootstrapper(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task EnsureAsync(CancellationToken ct = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS canonical_field_definition
            (
                name VARCHAR(255) PRIMARY KEY,
                data_type VARCHAR(50) NOT NULL,
                is_system_required BOOLEAN NOT NULL DEFAULT FALSE
            );
            """,
            ct);

        await _context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE field_change ADD COLUMN IF NOT EXISTS canonical_field_name VARCHAR(255);",
            ct);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE field_change SET canonical_field_name = field_name WHERE canonical_field_name IS NULL OR btrim(canonical_field_name) = '';",
            ct);

        foreach (var definition in CanonicalFieldDefaults.All)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO canonical_field_definition (name, data_type, is_system_required)
                VALUES ({definition.Name}, {definition.DataType.ToString()}, {definition.IsSystemRequired})
                ON CONFLICT (name) DO UPDATE
                SET data_type = EXCLUDED.data_type,
                    is_system_required = canonical_field_definition.is_system_required OR EXCLUDED.is_system_required;
                """,
                ct);
        }
    }
}
