namespace UPMS.Web.Templates;

using UPMS.Data;

/// <summary>
/// Full rendering context for tokenised templates.
/// Includes aggregate tokens plus the selected tickets for per-ticket loop expansion.
/// </summary>
public sealed class TemplateRenderContext
{
    public TemplateRenderContext(
        IReadOnlyDictionary<string, string?> globalTokens,
        IReadOnlyList<Ticket> tickets,
        string itsmSource,
        string company,
        DateTime asOfDate,
        IReadOnlyList<string> detailFields,
        string? requestedBy = null)
    {
        GlobalTokens = globalTokens ?? throw new ArgumentNullException(nameof(globalTokens));
        Tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        ItsmSource = itsmSource ?? throw new ArgumentNullException(nameof(itsmSource));
        Company = company ?? throw new ArgumentNullException(nameof(company));
        AsOfDate = asOfDate;
        DetailFields = detailFields ?? Array.Empty<string>();
        RequestedBy = requestedBy;
    }

    public IReadOnlyDictionary<string, string?> GlobalTokens { get; }

    public IReadOnlyList<Ticket> Tickets { get; }

    public string ItsmSource { get; }

    public string Company { get; }

    public DateTime AsOfDate { get; }

    public IReadOnlyList<string> DetailFields { get; }

    public string? RequestedBy { get; }
}
