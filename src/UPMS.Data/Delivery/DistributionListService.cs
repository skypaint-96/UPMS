using System.Net.Mail;
using Microsoft.EntityFrameworkCore;

namespace UPMS.Data.Delivery;

public class DistributionListService : IDistributionListService
{
    private readonly UpmsDbContext _context;

    public DistributionListService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<CompanyProfileSummary>> GetCompaniesAsync(CancellationToken ct = default)
    {
        return await _context.CompanyProfiles
            .AsNoTracking()
            .Select(company => new CompanyProfileSummary(
                company.Id,
                company.CompanyKey,
                company.DisplayName,
                company.DistributionLists.Count))
            .OrderBy(company => company.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DistributionListDetail>> GetListsAsync(string? company = null, CancellationToken ct = default)
    {
        var query = _context.DistributionLists
            .AsNoTracking()
            .Include(list => list.CompanyProfile)
            .Include(list => list.Recipients)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(company))
        {
            var companyKey = CompanyKeyNormalizer.Normalize(company);
            query = query.Where(list => list.CompanyProfile != null && list.CompanyProfile.CompanyKey == companyKey);
        }

        var lists = await query
            .OrderBy(list => list.CompanyProfile!.DisplayName)
            .ThenBy(list => list.Name)
            .ToListAsync(ct);

        return lists.Select(Map).ToList();
    }

    public async Task<DistributionListDetail?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var list = await _context.DistributionLists
            .AsNoTracking()
            .Include(row => row.CompanyProfile)
            .Include(row => row.Recipients)
            .FirstOrDefaultAsync(row => row.Id == id, ct);

        return list is null ? null : Map(list);
    }

    public async Task<DistributionListDetail> CreateAsync(SaveDistributionListCommand command, string? requestedBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var utcNow = DateTime.UtcNow;
        var companyProfile = await ResolveCompanyProfileAsync(command.CompanyName, utcNow, ct);
        var normalizedListName = NormalizeName(command.Name, nameof(command.Name));

        var duplicateExists = await _context.DistributionLists
            .AnyAsync(list => list.CompanyProfileId == companyProfile.Id && list.Name == normalizedListName, ct);

        if (duplicateExists)
            throw new InvalidOperationException($"Distribution list '{normalizedListName}' already exists for company '{companyProfile.DisplayName}'.");

        var requestedByName = string.IsNullOrWhiteSpace(requestedBy) ? "system" : requestedBy.Trim();
        var listId = Guid.NewGuid();

        var list = new DistributionList
        {
            Id = listId,
            CompanyProfileId = companyProfile.Id,
            Name = normalizedListName,
            Description = NormalizeOptional(command.Description),
            IsActive = command.IsActive,
            CreatedAt = utcNow,
            CreatedBy = requestedByName,
            UpdatedAt = utcNow,
            UpdatedBy = requestedByName,
            Recipients = NormalizeRecipients(listId, command.Recipients, utcNow)
        };

        _context.DistributionLists.Add(list);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(list.Id, ct))!;
    }

    public async Task<DistributionListDetail> UpdateAsync(Guid id, SaveDistributionListCommand command, string? requestedBy, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Distribution list ID is required.", nameof(id));

        ArgumentNullException.ThrowIfNull(command);

        var list = await _context.DistributionLists
            .Include(row => row.CompanyProfile)
            .Include(row => row.Recipients)
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"Distribution list '{id}' was not found.");

        var utcNow = DateTime.UtcNow;
        var previousCompanyProfileId = list.CompanyProfileId;
        var companyProfile = await ResolveCompanyProfileAsync(command.CompanyName, utcNow, ct);
        var normalizedListName = NormalizeName(command.Name, nameof(command.Name));

        var duplicateExists = await _context.DistributionLists
            .AnyAsync(row => row.Id != id && row.CompanyProfileId == companyProfile.Id && row.Name == normalizedListName, ct);

        if (duplicateExists)
            throw new InvalidOperationException($"Distribution list '{normalizedListName}' already exists for company '{companyProfile.DisplayName}'.");

        list.CompanyProfileId = companyProfile.Id;
        list.Name = normalizedListName;
        list.Description = NormalizeOptional(command.Description);
        list.IsActive = command.IsActive;
        list.UpdatedAt = utcNow;
        list.UpdatedBy = string.IsNullOrWhiteSpace(requestedBy) ? list.UpdatedBy : requestedBy.Trim();

        _context.DistributionListRecipients.RemoveRange(list.Recipients);
        list.Recipients = NormalizeRecipients(list.Id, command.Recipients, utcNow);

        await _context.SaveChangesAsync(ct);
        await CleanupOrphanCompanyProfileAsync(previousCompanyProfileId, ct);

        return (await GetByIdAsync(list.Id, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return;

        var list = await _context.DistributionLists
            .Include(row => row.Recipients)
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"Distribution list '{id}' was not found.");

        var companyProfileId = list.CompanyProfileId;

        _context.DistributionListRecipients.RemoveRange(list.Recipients);
        _context.DistributionLists.Remove(list);
        await _context.SaveChangesAsync(ct);
        await CleanupOrphanCompanyProfileAsync(companyProfileId, ct);
    }

    private async Task<CompanyProfile> ResolveCompanyProfileAsync(string companyName, DateTime utcNow, CancellationToken ct)
    {
        var normalizedDisplayName = NormalizeName(companyName, nameof(companyName));
        var companyKey = CompanyKeyNormalizer.Normalize(normalizedDisplayName);
        if (string.IsNullOrWhiteSpace(companyKey))
            throw new InvalidOperationException("Company name could not be normalized into a company key.");

        var existing = await _context.CompanyProfiles
            .FirstOrDefaultAsync(company => company.CompanyKey == companyKey, ct);

        if (existing is not null)
        {
            if (!string.Equals(existing.DisplayName, normalizedDisplayName, StringComparison.Ordinal))
            {
                existing.DisplayName = normalizedDisplayName;
                existing.UpdatedAt = utcNow;
                await _context.SaveChangesAsync(ct);
            }

            return existing;
        }

        var company = new CompanyProfile
        {
            Id = Guid.NewGuid(),
            CompanyKey = companyKey,
            DisplayName = normalizedDisplayName,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        _context.CompanyProfiles.Add(company);
        await _context.SaveChangesAsync(ct);
        return company;
    }

    private async Task CleanupOrphanCompanyProfileAsync(Guid companyProfileId, CancellationToken ct)
    {
        if (companyProfileId == Guid.Empty)
            return;

        var companyProfile = await _context.CompanyProfiles
            .Include(company => company.DistributionLists)
            .FirstOrDefaultAsync(company => company.Id == companyProfileId, ct);

        if (companyProfile is null)
            return;

        if (companyProfile.DistributionLists.Count > 0)
            return;

        _context.CompanyProfiles.Remove(companyProfile);
        await _context.SaveChangesAsync(ct);
    }

    private static DistributionListDetail Map(DistributionList list)
    {
        var recipients = list.Recipients
            .OrderBy(recipient => recipient.SortOrder)
            .ThenBy(recipient => recipient.Endpoint, StringComparer.OrdinalIgnoreCase)
            .Select(recipient => new DistributionListRecipientModel(
                recipient.Id,
                recipient.Channel,
                recipient.Endpoint,
                recipient.DisplayName,
                recipient.MetadataJson,
                recipient.IsActive,
                recipient.SortOrder))
            .ToList();

        return new DistributionListDetail(
            list.Id,
            list.CompanyProfileId,
            list.CompanyProfile?.CompanyKey ?? string.Empty,
            list.CompanyProfile?.DisplayName ?? string.Empty,
            list.Name,
            list.Description,
            list.IsActive,
            recipients.Count,
            recipients.Count(recipient => recipient.IsActive),
            recipients,
            list.CreatedAt,
            list.CreatedBy,
            list.UpdatedAt,
            list.UpdatedBy);
    }

    private static List<DistributionListRecipient> NormalizeRecipients(Guid distributionListId, IReadOnlyList<DistributionListRecipientInput>? recipients, DateTime utcNow)
    {
        if (recipients is null || recipients.Count == 0)
            return [];

        return recipients
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient.Endpoint))
            .Select((recipient, index) =>
            {
                var channel = NormalizeOptional(recipient.Channel) ?? ReportDeliveryChannels.Email;
                var normalizedChannel = channel.ToLowerInvariant();
                var endpoint = NormalizeEndpoint(normalizedChannel, recipient.Endpoint);

                return new DistributionListRecipient
                {
                    Id = Guid.NewGuid(),
                    DistributionListId = distributionListId,
                    Channel = normalizedChannel,
                    Endpoint = endpoint,
                    DisplayName = NormalizeOptional(recipient.DisplayName),
                    MetadataJson = NormalizeOptional(recipient.MetadataJson),
                    IsActive = recipient.IsActive,
                    SortOrder = recipient.SortOrder < 0 ? index : recipient.SortOrder,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                };
            })
            .DistinctBy(recipient => $"{recipient.Channel}:{recipient.Endpoint}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeEndpoint(string channel, string endpoint)
    {
        var normalized = NormalizeName(endpoint, nameof(endpoint));

        if (string.Equals(channel, ReportDeliveryChannels.Email, StringComparison.OrdinalIgnoreCase))
        {
            _ = new MailAddress(normalized);
            return normalized.ToLowerInvariant();
        }

        return normalized;
    }

    private static string NormalizeName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
