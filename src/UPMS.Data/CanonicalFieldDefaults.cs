namespace UPMS.Data;

public sealed record CanonicalFieldDefaultDefinition(
    string Name,
    CanonicalFieldDataType DataType,
    bool IsSystemRequired = false);

public static class CanonicalFieldDefaults
{
    public static readonly IReadOnlyList<CanonicalFieldDefaultDefinition> All =
    [
        new("Active", CanonicalFieldDataType.Boolean),
        new("Assigned To", CanonicalFieldDataType.Text),
        new("Assignment Group", CanonicalFieldDataType.Text),
        new("Business Service", CanonicalFieldDataType.Text),
        new("Category", CanonicalFieldDataType.Text),
        new("Cause Notes", CanonicalFieldDataType.Text),
        new("Closed At", CanonicalFieldDataType.DateTime),
        new("Closed By", CanonicalFieldDataType.Text),
        new("Close Notes", CanonicalFieldDataType.Text),
        new("Cmdb Ci", CanonicalFieldDataType.Text),
        new("Comments", CanonicalFieldDataType.Text),
        new("Comments And Work Notes", CanonicalFieldDataType.Text),
        new("Company", CanonicalFieldDataType.Text, IsSystemRequired: true),
        new("Confirmed At", CanonicalFieldDataType.DateTime),
        new("Confirmed By", CanonicalFieldDataType.Text),
        new("Correlation Display", CanonicalFieldDataType.Text),
        new("Correlation Id", CanonicalFieldDataType.Text),
        new("Description", CanonicalFieldDataType.Text),
        new("First Reported By Task", CanonicalFieldDataType.Text),
        new("Fix Notes", CanonicalFieldDataType.Text),
        new("Knowledge", CanonicalFieldDataType.Text),
        new("Major Problem", CanonicalFieldDataType.Text),
        new("Number", CanonicalFieldDataType.Text, IsSystemRequired: true),
        new("Opened At", CanonicalFieldDataType.DateTime),
        new("Opened By", CanonicalFieldDataType.Text),
        new("Priority", CanonicalFieldDataType.Text),
        new("Related Incidents", CanonicalFieldDataType.Text),
        new("Resolution Code", CanonicalFieldDataType.Text),
        new("Resolved At", CanonicalFieldDataType.DateTime),
        new("Resolved By", CanonicalFieldDataType.Text),
        new("Service Offering", CanonicalFieldDataType.Text),
        new("Short Description", CanonicalFieldDataType.Text),
        new("State", CanonicalFieldDataType.Text),
        new("Subcategory", CanonicalFieldDataType.Text),
        new("Created By", CanonicalFieldDataType.Text),
        new("Created On", CanonicalFieldDataType.DateTime),
        new("Updated By", CanonicalFieldDataType.Text),
        new("Updated On", CanonicalFieldDataType.DateTime),
        new("Investigation Driver", CanonicalFieldDataType.Text),
        new("Root Cause Code", CanonicalFieldDataType.Text),
        new("Root Cause Date", CanonicalFieldDataType.DateTime),
        new("Workaround", CanonicalFieldDataType.Text)
    ];

    public static bool IsSystemRequiredName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return All.Any(d => d.IsSystemRequired && string.Equals(d.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
