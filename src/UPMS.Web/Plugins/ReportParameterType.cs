namespace UPMS.Web.Plugins;

public enum ReportParameterType
{
    Text,
    Date,
    DateRange,
    Select,
    MultiSelect,
    Boolean,

    /// <summary>
    /// Select an ITSM source from the <c>itsm_source</c> table.
    /// Rendered by <see cref="UPMS.Web.Components.Shared.ReportParameterForm"/>.
    /// </summary>
    ItsmSource
}
