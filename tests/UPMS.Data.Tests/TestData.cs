namespace UPMS.Data.Tests;

using System;

/// <summary>
/// Constants and test data used across test classes.
/// </summary>
public static class TestData
{
    // Company Names
    public static class Companies
    {
        public const string Company1 = "Acme Corp";
        public const string Company2 = "TechWorks Inc";
        public const string Company3 = "Global Solutions Ltd";
    }

    // ITSM Sources
    public static class Itsm
    {
        public const string ServiceNow = "servicenow";
        public const string Jira = "jira";
    }

    // Ticket Keys
    public static class TicketKeys
    {
        public const string Ticket1 = "INC0001234";
        public const string Ticket2 = "INC0001235";
        public const string Ticket3 = "INC0001236";
        public const string Ticket4 = "INC0001237";
        public const string Ticket5 = "INC0001238";
    }

    // Field Names
    public static class Fields
    {
        public const string Status = "Status";
        public const string Priority = "Priority";
        public const string Assignee = "Assignee";
        public const string Resolution = "Resolution";
        public const string Description = "Description";
    }

    // Field Values
    public static class FieldValues
    {
        public const string StatusOpen = "Open";
        public const string StatusInProgress = "In Progress";
        public const string StatusResolved = "Resolved";
        public const string PriorityHigh = "High";
        public const string PriorityMedium = "Medium";
        public const string PriorityLow = "Low";
    }

    // Dates
    public static class Dates
    {
        public static readonly DateTime OlderSnapshotDate = new(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime NewSnapshotDate = new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime ReferenceDate = NewSnapshotDate;
    }

    // Canonical field mapping test data
    public static class SourceFields
    {
        // ServiceNow source field names
        public const string ServiceNowIncidentState = "incident_state";
        public const string ServiceNowAssignedTo = "assigned_to";
        public const string ServiceNowShortDescription = "short_description";

        // Jira source field names
        public const string JiraStatus = "status";
        public const string JiraAssignee = "assignee";
        public const string JiraSummary = "summary";

        // Unknown field (no mapping)
        public const string UnknownField = "custom_field_xyz";
    }

    public static class CanonicalFields
    {
        public const string Status = "Status";
        public const string Assignee = "Assignee";
        public const string Summary = "Summary";
    }
}
