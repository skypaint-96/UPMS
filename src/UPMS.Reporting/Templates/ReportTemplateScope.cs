namespace UPMS.Reporting.Templates;

using System.Text.Json.Serialization;

public sealed class ReportTemplateScope
{
    public string[] ItsmSources { get; init; } = [];
    public string[] Companies { get; init; } = [];
    public ReportTemplateScopeCombination[] ItsmSourceCompanies { get; init; } = [];

    [JsonIgnore]
    public bool IsGlobal => ItsmSources.Length == 0
        && Companies.Length == 0
        && ItsmSourceCompanies.Length == 0;
}

public sealed class ReportTemplateScopeCombination
{
    public string ItsmSource { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
}
