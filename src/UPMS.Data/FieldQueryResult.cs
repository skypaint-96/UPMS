namespace UPMS.Data;

/// <summary>
/// DTO for field query results from the database.
/// </summary>
internal class FieldQueryResult
{
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
}
