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
        foreach (var definition in CanonicalFieldDefaults.All)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO canonical_field_definition (name, data_type, is_system_required)
                VALUES ({definition.Name}, {definition.DataType.ToString()}, {definition.IsSystemRequired})
                ON CONFLICT (name) DO UPDATE
                SET is_system_required = canonical_field_definition.is_system_required OR EXCLUDED.is_system_required;
                """,
                ct);
        }
    }
}
