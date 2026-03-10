namespace UPMS.Reporting.Plugins.Documents;

using System.Text;

internal static class MinimalPdfGenerator
{
    public static byte[] Generate(IReadOnlyList<string> lines)
    {
        const int maxLinesPerPage = 48;
        var pages = lines.Chunk(maxLinesPerPage).Select(chunk => chunk.ToList()).ToList();
        if (pages.Count == 0)
            pages.Add([string.Empty]);

        List<string> objects = [];
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");

        StringBuilder kids = new();
        for (var i = 0; i < pages.Count; i++)
        {
            var pageObjectNumber = 4 + (i * 2);
            kids.Append($"{pageObjectNumber} 0 R ");
        }

        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (var i = 0; i < pages.Count; i++)
        {
            var pageObjectNumber = 4 + (i * 2);
            var contentObjectNumber = 5 + (i * 2);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectNumber} 0 R >>");

            var stream = BuildContentStream(pages[i]);
            var length = Encoding.ASCII.GetByteCount(stream);
            objects.Add($"<< /Length {length} >>\nstream\n{stream}\nendstream");
        }

        StringBuilder pdf = new();
        pdf.AppendLine("%PDF-1.4");
        List<int> offsets = [0];

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{i + 1} 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefStart = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        for (var i = 1; i < offsets.Count; i++)
        {
            pdf.AppendLine($"{offsets[i]:D10} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefStart.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string BuildContentStream(IReadOnlyList<string> lines)
    {
        StringBuilder sb = new();
        sb.AppendLine("BT");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("50 760 Td");

        foreach (var line in lines)
        {
            sb.Append('(')
                .Append(Escape(line))
                .AppendLine(") Tj");
            sb.AppendLine("T*");
        }

        sb.AppendLine("ET");
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        var ascii = new string((value ?? string.Empty)
            .Select(ch => ch is < ' ' or > '~' ? '?' : ch)
            .ToArray());

        return ascii
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
