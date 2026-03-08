namespace UPMS.Web.Services;

using Microsoft.EntityFrameworkCore;
using UPMS.Data;

public sealed class CanonicalValueSuggestionService : ICanonicalValueSuggestionService
{
    private readonly UpmsDbContext _context;

    public CanonicalValueSuggestionService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        string canonicalFieldName,
        IEnumerable<string>? itsmSources = null,
        int max = 12,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(canonicalFieldName))
            return Array.Empty<string>();

        max = Math.Clamp(max, 1, 50);

        string normalizedField = canonicalFieldName.Trim();
        var sourceNames = (itsmSources ?? Array.Empty<string>())
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(static s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.Equals(normalizedField, "Company", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedField, "company", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedField, "company_name", StringComparison.OrdinalIgnoreCase))
        {
            var companyQuery = from st in _context.SnapshotTickets.AsNoTracking()
                               join snapshot in _context.Snapshots.AsNoTracking() on st.SnapshotId equals snapshot.Id
                               select new { st.CompanyName, snapshot.ItsmSource };

            if (sourceNames.Count > 0)
            {
                companyQuery = companyQuery.Where(row => sourceNames.Contains(row.ItsmSource));
            }

            return await companyQuery
                .Select(row => row.CompanyName)
                .Where(value => value != null && value != string.Empty)
                .Distinct()
                .OrderBy(value => value)
                .Take(max)
                .ToListAsync(ct);
        }

        var aliases = CanonicalFieldCatalog.GetAliases(normalizedField)
            .Concat([normalizedField])
            .Where(static alias => !string.IsNullOrWhiteSpace(alias))
            .Select(static alias => alias.Trim().ToLower())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (aliases.Count == 0)
            return Array.Empty<string>();

        var fieldQuery = from fc in _context.FieldChanges.AsNoTracking()
                         join snapshot in _context.Snapshots.AsNoTracking() on fc.SnapshotId equals snapshot.Id
                         select new
                         {
                             fc.FieldValue,
                             fc.FieldName,
                             fc.CanonicalFieldName,
                             snapshot.ItsmSource
                         };

        if (sourceNames.Count > 0)
        {
            fieldQuery = fieldQuery.Where(row => sourceNames.Contains(row.ItsmSource));
        }

        return await fieldQuery
            .Where(row => row.FieldValue != null && row.FieldValue != string.Empty
                && ((row.CanonicalFieldName != null && aliases.Contains(row.CanonicalFieldName.ToLower()))
                    || aliases.Contains(row.FieldName.ToLower())))
            .Select(row => row.FieldValue!)
            .Distinct()
            .OrderBy(value => value)
            .Take(max)
            .ToListAsync(ct);
    }
}
