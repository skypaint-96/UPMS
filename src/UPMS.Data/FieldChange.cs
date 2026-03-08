namespace UPMS.Data;

using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents a single observed field value at a point in time (<c>field_change</c> table).
/// </summary>
public class FieldChange
{
    /// <summary>Parameterless constructor required by EF Core and Dapper.</summary>
    public FieldChange() { }

    public long Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? CanonicalFieldName { get; set; }
    public string? FieldValue { get; set; }
    public DateTime ObservedAt { get; set; }
    public Guid SnapshotId { get; set; }

    [NotMapped]
    public string DisplayFieldName => string.IsNullOrWhiteSpace(CanonicalFieldName) ? FieldName : CanonicalFieldName!;

    [NotMapped]
    public CanonicalFieldDataType? RegisteredDataType { get; set; }

    [NotMapped]
    public bool? IsValueValid { get; set; }
}
