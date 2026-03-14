namespace UPMS.Data.Delivery;

public sealed record CompanyProfileSummary(
    Guid Id,
    string CompanyKey,
    string DisplayName,
    int DistributionListCount);

public sealed record DistributionListRecipientInput(
    string Channel,
    string Endpoint,
    string? DisplayName,
    string? MetadataJson,
    bool IsActive,
    int SortOrder);

public sealed record SaveDistributionListCommand(
    string CompanyName,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<DistributionListRecipientInput> Recipients);

public sealed record DistributionListRecipientModel(
    Guid Id,
    string Channel,
    string Endpoint,
    string? DisplayName,
    string? MetadataJson,
    bool IsActive,
    int SortOrder);

public sealed record DistributionListDetail(
    Guid Id,
    Guid CompanyProfileId,
    string CompanyKey,
    string CompanyName,
    string Name,
    string? Description,
    bool IsActive,
    int RecipientCount,
    int ActiveRecipientCount,
    IReadOnlyList<DistributionListRecipientModel> Recipients,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime UpdatedAt,
    string? UpdatedBy);
