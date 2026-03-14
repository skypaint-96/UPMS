namespace UPMS.Data.Delivery;

public interface IDistributionListService
{
    Task<IReadOnlyList<CompanyProfileSummary>> GetCompaniesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DistributionListDetail>> GetListsAsync(string? company = null, CancellationToken ct = default);
    Task<DistributionListDetail?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DistributionListDetail> CreateAsync(SaveDistributionListCommand command, string? requestedBy, CancellationToken ct = default);
    Task<DistributionListDetail> UpdateAsync(Guid id, SaveDistributionListCommand command, string? requestedBy, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
