namespace UPMS.Web;

public static class CanonicalFieldCatalog
{
    public static readonly IReadOnlyList<string> Names =
    [
        "Active",
        "Assigned To",
        "Assignment Group",
        "Business Service",
        "Category",
        "Cause Notes",
        "Closed At",
        "Closed By",
        "Close Notes",
        "Cmdb Ci",
        "Comments",
        "Comments And Work Notes",
        "Company",
        "Confirmed At",
        "Confirmed By",
        "Correlation Display",
        "Correlation Id",
        "Description",
        "First Reported By Task",
        "Fix Notes",
        "Knowledge",
        "Major Problem",
        "Number",
        "Opened At",
        "Opened By",
        "Priority",
        "Related Incidents",
        "Resolution Code",
        "Resolved At",
        "Resolved By",
        "Service Offering",
        "Short Description",
        "State",
        "Subcategory",
        "Created By",
        "Created On",
        "Updated By",
        "Updated On",
        "Investigation Driver",
        "Root Cause Code",
        "Root Cause Date",
        "Workaround"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AliasMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Active"] = ["Active", "active"],
            ["Assigned To"] = ["Assigned To", "assigned_to", "assignee", "AssignedTo"],
            ["Assignment Group"] = ["Assignment Group", "assignment_group", "AssignmentGroup"],
            ["Business Service"] = ["Business Service", "business_service"],
            ["Category"] = ["Category", "category"],
            ["Cause Notes"] = ["Cause Notes", "cause_notes"],
            ["Closed At"] = ["Closed At", "closed_at", "closed_date"],
            ["Closed By"] = ["Closed By", "closed_by"],
            ["Close Notes"] = ["Close Notes", "close_notes"],
            ["Cmdb Ci"] = ["Cmdb Ci", "cmdb_ci"],
            ["Comments"] = ["Comments", "comments"],
            ["Comments And Work Notes"] = ["Comments And Work Notes", "comments_and_work_notes"],
            ["Company"] = ["Company", "company"],
            ["Confirmed At"] = ["Confirmed At", "confirmed_at"],
            ["Confirmed By"] = ["Confirmed By", "confirmed_by"],
            ["Correlation Display"] = ["Correlation Display", "correlation_display"],
            ["Correlation Id"] = ["Correlation Id", "correlation_id"],
            ["Description"] = ["Description", "description"],
            ["First Reported By Task"] = ["First Reported By Task", "first_reported_by_task"],
            ["Fix Notes"] = ["Fix Notes", "fix_notes"],
            ["Knowledge"] = ["Knowledge", "knowledge"],
            ["Major Problem"] = ["Major Problem", "major_problem"],
            ["Number"] = ["Number", "number", "ticket_number", "ticket_key"],
            ["Opened At"] = ["Opened At", "opened_at", "opened_date", "created_at", "created_date", "Created On"],
            ["Opened By"] = ["Opened By", "opened_by"],
            ["Priority"] = ["Priority", "priority"],
            ["Related Incidents"] = ["Related Incidents", "related_incidents"],
            ["Resolution Code"] = ["Resolution Code", "resolution_code"],
            ["Resolved At"] = ["Resolved At", "resolved_at", "resolved_date"],
            ["Resolved By"] = ["Resolved By", "resolved_by"],
            ["Service Offering"] = ["Service Offering", "service_offering"],
            ["Short Description"] = ["Short Description", "short_description", "title"],
            ["State"] = ["State", "state", "status", "incident_state"],
            ["Subcategory"] = ["Subcategory", "subcategory", "sub_category"],
            ["Created By"] = ["Created By", "created_by"],
            ["Created On"] = ["Created On", "created_on", "created_at", "created_date"],
            ["Updated By"] = ["Updated By", "updated_by"],
            ["Updated On"] = ["Updated On", "updated_on", "updated_at", "updated_date", "last_updated"],
            ["Investigation Driver"] = ["Investigation Driver", "investigation_driver"],
            ["Root Cause Code"] = ["Root Cause Code", "root_cause_code"],
            ["Root Cause Date"] = ["Root Cause Date", "root_cause_date"],
            ["Workaround"] = ["Workaround", "workaround"]
        };

    public static IReadOnlyList<string> GetAliases(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return Array.Empty<string>();

        if (AliasMap.TryGetValue(fieldName, out var aliases))
            return aliases;

        foreach (var pair in AliasMap)
        {
            if (pair.Value.Any(alias => string.Equals(alias, fieldName, StringComparison.OrdinalIgnoreCase)))
                return pair.Value;
        }

        return [fieldName];
    }
}
