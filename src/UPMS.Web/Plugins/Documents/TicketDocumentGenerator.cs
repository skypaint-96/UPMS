namespace UPMS.Web.Plugins.Documents;

using System.IO.Compression;
using System.Text;
using UPMS.Data;
using UPMS.Web.Reporting;

public static class TicketDocumentGenerator
{
    public static byte[] GenerateDocx(
        string company,
        string itsmSource,
        DateTime asOfDate,
        IReadOnlyList<TicketLifecycleInfo> tickets,
        IReadOnlyList<string> detailFields,
        IReadOnlyDictionary<string, IReadOnlyList<FieldChange>> historyByTicket)
    {
        var lines = BuildDocumentLines(company, itsmSource, asOfDate, tickets, detailFields, historyByTicket);

        using MemoryStream ms = new();
        using (ZipArchive zip = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "[Content_Types].xml", ContentTypesXml());
            AddEntry(zip, "_rels/.rels", RootRelsXml());
            AddEntry(zip, "docProps/core.xml", CorePropertiesXml(company, itsmSource));
            AddEntry(zip, "docProps/app.xml", AppPropertiesXml());
            AddEntry(zip, "word/document.xml", DocumentXml(lines));
            AddEntry(zip, "word/styles.xml", StylesXml());
        }

        return ms.ToArray();
    }

    public static byte[] GeneratePdf(
        string company,
        string itsmSource,
        DateTime asOfDate,
        IReadOnlyList<TicketLifecycleInfo> tickets,
        IReadOnlyList<string> detailFields,
        IReadOnlyDictionary<string, IReadOnlyList<FieldChange>> historyByTicket)
    {
        var lines = BuildDocumentLines(company, itsmSource, asOfDate, tickets, detailFields, historyByTicket)
            .Select(ToPdfLine)
            .ToList();

        return MinimalPdfGenerator.Generate(lines);
    }

    private static IReadOnlyList<DocumentLine> BuildDocumentLines(
        string company,
        string itsmSource,
        DateTime asOfDate,
        IReadOnlyList<TicketLifecycleInfo> tickets,
        IReadOnlyList<string> detailFields,
        IReadOnlyDictionary<string, IReadOnlyList<FieldChange>> historyByTicket)
    {
        List<DocumentLine> lines =
        [
            new DocumentLine("UPMS Ticket Export", Bold: true, SizeHalfPoints: 32),
            new DocumentLine($"Company: {company} | Source: {itsmSource} | As of: {asOfDate:yyyy-MM-dd}"),
            new DocumentLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"),
            new DocumentLine(string.Empty)
        ];

        if (tickets.Count == 0)
        {
            lines.Add(new DocumentLine("No tickets matched the selected scope."));
            return lines;
        }

        for (var i = 0; i < tickets.Count; i++)
        {
            var lifecycle = tickets[i];
            var ticket = lifecycle.Ticket;
            var number = TicketFieldHelpers.GetFieldValue(ticket, "Number", fallback: ticket.TicketKey);

            lines.Add(new DocumentLine($"Ticket {number}", Bold: true, SizeHalfPoints: 28, PageBreakBefore: i > 0));
            lines.Add(new DocumentLine($"Ticket Key: {ticket.TicketKey}"));
            lines.Add(new DocumentLine($"Company: {ticket.CompanyName}"));
            lines.Add(new DocumentLine($"State: {lifecycle.Status}"));
            lines.Add(new DocumentLine($"Priority: {lifecycle.Priority}"));
            lines.Add(new DocumentLine($"Opened At: {FormatDate(lifecycle.OpenedAt)}"));
            lines.Add(new DocumentLine($"Resolved At: {FormatDate(lifecycle.ResolvedAt)}"));
            lines.Add(new DocumentLine($"Updated On: {FormatDate(lifecycle.UpdatedAt)}"));
            lines.Add(new DocumentLine($"Age (days): {FormatAge(lifecycle.AgeDays)}"));

            foreach (var field in detailFields)
            {
                if (field.Equals("State", StringComparison.OrdinalIgnoreCase)
                    || field.Equals("Priority", StringComparison.OrdinalIgnoreCase)
                    || field.Equals("Opened At", StringComparison.OrdinalIgnoreCase)
                    || field.Equals("Resolved At", StringComparison.OrdinalIgnoreCase)
                    || field.Equals("Updated On", StringComparison.OrdinalIgnoreCase)
                    || field.Equals("Number", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = TicketFieldHelpers.GetFieldValue(ticket, field, fallback: string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                    lines.Add(new DocumentLine($"{field}: {value}"));
            }

            if (historyByTicket.TryGetValue(ticket.TicketKey, out var history) && history.Count > 0)
            {
                lines.Add(new DocumentLine(string.Empty));
                lines.Add(new DocumentLine("Field Change History", Bold: true, SizeHalfPoints: 24));
                foreach (var change in history.Take(50))
                {
                    lines.Add(new DocumentLine($"{change.ObservedAt:yyyy-MM-dd HH:mm} | {change.FieldName} = {change.FieldValue ?? "(blank)"}"));
                }
            }

            lines.Add(new DocumentLine(string.Empty));
        }

        return lines;
    }

    private static string ToPdfLine(DocumentLine line)
    {
        if (line.Bold && line.SizeHalfPoints >= 28)
            return line.Text.ToUpperInvariant();

        return line.Text;
    }

    private static string ContentTypesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
          <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
        </Types>
        """;

    private static string RootRelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
        </Relationships>
        """;

    private static string CorePropertiesXml(string company, string itsmSource)
    {
        var now = DateTime.UtcNow.ToString("s") + "Z";
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
                               xmlns:dc="http://purl.org/dc/elements/1.1/"
                               xmlns:dcterms="http://purl.org/dc/terms/"
                               xmlns:dcmitype="http://purl.org/dc/dcmitype/"
                               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <dc:title>UPMS Ticket Export - {Xml(company)}</dc:title>
              <dc:subject>{Xml(itsmSource)}</dc:subject>
              <dc:creator>UPMS</dc:creator>
              <cp:lastModifiedBy>UPMS</cp:lastModifiedBy>
              <dcterms:created xsi:type="dcterms:W3CDTF">{now}</dcterms:created>
              <dcterms:modified xsi:type="dcterms:W3CDTF">{now}</dcterms:modified>
            </cp:coreProperties>
            """;
    }

    private static string AppPropertiesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
                    xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
          <Application>UPMS</Application>
        </Properties>
        """;

    private static string StylesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
            <w:name w:val="Normal"/>
            <w:qFormat/>
          </w:style>
        </w:styles>
        """;

    private static string DocumentXml(IReadOnlyList<DocumentLine> lines)
    {
        StringBuilder body = new();
        foreach (var line in lines)
        {
            if (line.PageBreakBefore)
            {
                body.AppendLine("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
            }

            body.AppendLine(BuildParagraph(line));
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                {body}
                <w:sectPr>
                  <w:pgSz w:w="12240" w:h="15840"/>
                  <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/>
                </w:sectPr>
              </w:body>
            </w:document>
            """;
    }

    private static string BuildParagraph(DocumentLine line)
    {
        if (string.IsNullOrEmpty(line.Text))
            return "<w:p/>";

        var rPr = line.Bold || line.SizeHalfPoints != 22
            ? $"<w:rPr>{(line.Bold ? "<w:b/>" : string.Empty)}<w:sz w:val=\"{line.SizeHalfPoints}\"/><w:szCs w:val=\"{line.SizeHalfPoints}\"/></w:rPr>"
            : string.Empty;

        return $"<w:p><w:r>{rPr}<w:t xml:space=\"preserve\">{Xml(line.Text)}</w:t></w:r></w:p>";
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    private static string FormatAge(double? value) => value?.ToString("0.0") ?? string.Empty;
    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

    private sealed record DocumentLine(string Text, bool Bold = false, int SizeHalfPoints = 22, bool PageBreakBefore = false);
}
