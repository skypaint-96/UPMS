namespace UPMS.Web.Plugins.Email;

using System.Text;
using UPMS.Data;

public static class EmailDraftBuilder
{
    public static string BuildSubject(string templateName, string company, string itsmSource, DateTime asOfDate, IReadOnlyList<Ticket> tickets)
    {
        var normalizedSource = itsmSource.ToUpperInvariant();
        return templateName switch
        {
            EmailTemplateRenderer.TemplateSummary => $"UPMS Monthly Summary - {company} - {normalizedSource} - {asOfDate:yyyy-MM-dd}",
            EmailTemplateRenderer.TemplateEscalation => $"UPMS Escalation Notice - {company} - {tickets.Count} ticket(s) - {asOfDate:yyyy-MM-dd}",
            _ => $"UPMS Report - {company} - {normalizedSource} - {asOfDate:yyyy-MM-dd}"
        };
    }

    public static byte[] BuildEml(string subject, string htmlBody, string? to = null, string? cc = null)
    {
        var bodyBase64 = WrapBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody)));
        var eml = string.Join("\r\n",
        [
            "X-Unsent: 1",
            $"To: {to ?? string.Empty}",
            $"Cc: {cc ?? string.Empty}",
            $"Subject: {subject}",
            "MIME-Version: 1.0",
            "Content-Type: text/html; charset=utf-8",
            "Content-Transfer-Encoding: base64",
            string.Empty,
            bodyBase64,
            string.Empty
        ]);

        return Encoding.UTF8.GetBytes(eml);
    }

    private static string WrapBase64(string value)
    {
        StringBuilder sb = new();
        for (var i = 0; i < value.Length; i += 76)
        {
            var chunkLength = Math.Min(76, value.Length - i);
            sb.AppendLine(value.Substring(i, chunkLength));
        }

        return sb.ToString().TrimEnd();
    }
}
