namespace UPMS.Web.Templates;

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UPMS.Data;

public static class TemplateTokenRenderer
{
    private const string PowerPointSlideRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide";
    private const string PowerPointSlideContentType = "application/vnd.openxmlformats-officedocument.presentationml.slide+xml";

    private static readonly Regex TokenRegex = new(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex PerTicketBlockRegex = new(
        @"\{\{\s*start\s+per\s+ticket(?<directive>.*?)\}\}(?<body>.*?)\{\{\s*end\s+per\s+ticket\s*\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ScopeRegex = new(@"\b(?:scope|mode)\s*=\s*(section|page|slide)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ClauseRegex = new(@"^\s*(?<field>[A-Za-z0-9_.-]+)\s*(?<op>!=|!~|=|~)\s*(?<value>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex SlideEntryRegex = new(@"^ppt/slides/slide(?<number>\d+)\.xml$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SlideRelsEntryRegex = new(@"^ppt/slides/_rels/slide(?<number>\d+)\.xml\.rels$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SlidePartNameRegex = new(@"^/ppt/slides/slide\d+\.xml$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RelationshipIdNumberRegex = new(@"\d+", RegexOptions.Compiled);

    public static string RenderText(string templateContent, IReadOnlyDictionary<string, string?> tokens)
    {
        var context = new TemplateRenderContext(tokens, Array.Empty<Ticket>(), string.Empty, string.Empty, DateTime.UtcNow, Array.Empty<string>());
        return RenderText(templateContent, context, string.Empty, entryName: null, currentTicketState: null, allowSlideScopedBlocks: false);
    }

    public static string RenderText(string templateContent, TemplateRenderContext context)
        => RenderText(templateContent, context, ".txt", entryName: null, currentTicketState: null, allowSlideScopedBlocks: false);

    public static byte[] RenderBytes(byte[] templateContent, string extension, IReadOnlyDictionary<string, string?> tokens)
    {
        var context = new TemplateRenderContext(tokens, Array.Empty<Ticket>(), string.Empty, string.Empty, DateTime.UtcNow, Array.Empty<string>());
        return RenderBytes(templateContent, extension, context);
    }

    public static byte[] RenderBytes(byte[] templateContent, string extension, TemplateRenderContext context)
    {
        var normalized = ReportTemplateContentTypeMapper.Normalize(extension);
        if (ReportTemplateContentTypeMapper.IsTextLike(normalized))
        {
            var text = Encoding.UTF8.GetString(templateContent);
            return Encoding.UTF8.GetBytes(RenderText(text, context, normalized, entryName: null, currentTicketState: null, allowSlideScopedBlocks: false));
        }

        if (ReportTemplateContentTypeMapper.IsOoxmlPackage(normalized))
        {
            return normalized.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
                ? RenderPowerPointPackage(templateContent, context)
                : RenderOoxmlPackage(templateContent, normalized, context);
        }

        return templateContent;
    }

    private static byte[] RenderOoxmlPackage(byte[] templateContent, string extension, TemplateRenderContext context)
    {
        using MemoryStream input = new(templateContent);
        using ZipArchive source = new(input, ZipArchiveMode.Read, leaveOpen: false);
        using MemoryStream output = new();
        using (ZipArchive target = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var targetEntry = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var entryInput = entry.Open();
                using var entryOutput = targetEntry.Open();

                if (ShouldTokenReplace(entry.FullName))
                {
                    using StreamReader reader = new(entryInput, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                    var text = reader.ReadToEnd();
                    var rendered = RenderText(text, context, extension, entry.FullName, currentTicketState: null, allowSlideScopedBlocks: false);
                    using StreamWriter writer = new(entryOutput, new UTF8Encoding(false), leaveOpen: true);
                    writer.Write(rendered);
                }
                else
                {
                    entryInput.CopyTo(entryOutput);
                }
            }
        }

        return output.ToArray();
    }

    private static byte[] RenderPowerPointPackage(byte[] templateContent, TemplateRenderContext context)
    {
        using MemoryStream input = new(templateContent);
        using ZipArchive source = new(input, ZipArchiveMode.Read, leaveOpen: false);

        var slideEntries = source.Entries
            .Where(entry => SlideEntryRegex.IsMatch(entry.FullName))
            .OrderBy(entry => ExtractTrailingNumber(entry.FullName, SlideEntryRegex))
            .ToList();

        var renderedSlides = new List<RenderedPowerPointSlide>();
        foreach (var slideEntry in slideEntries)
        {
            string slideXml = ReadEntryText(slideEntry);
            string? relsText = null;
            string relsPath = $"ppt/slides/_rels/{Path.GetFileName(slideEntry.FullName)}.rels";
            var relsEntry = source.GetEntry(relsPath);
            if (relsEntry is not null)
                relsText = ReadEntryText(relsEntry);

            var expandedSlides = ExpandPowerPointSlide(slideXml, slideEntry.FullName, context);
            foreach (var expanded in expandedSlides)
            {
                renderedSlides.Add(new RenderedPowerPointSlide(expanded, relsText));
            }
        }

        if (renderedSlides.Count == 0 && slideEntries.Count > 0)
        {
            var firstSlide = slideEntries[0];
            renderedSlides.Add(new RenderedPowerPointSlide(
                RenderText(ReadEntryText(firstSlide), context, ".pptx", firstSlide.FullName, currentTicketState: null, allowSlideScopedBlocks: false),
                source.GetEntry($"ppt/slides/_rels/{Path.GetFileName(firstSlide.FullName)}.rels") is ZipArchiveEntry firstRels
                    ? ReadEntryText(firstRels)
                    : null));
        }

        using MemoryStream output = new();
        using (ZipArchive target = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                if (ShouldSkipPowerPointEntry(entry.FullName))
                    continue;

                var targetEntry = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var sourceStream = entry.Open();
                using var targetStream = targetEntry.Open();
                sourceStream.CopyTo(targetStream);
            }

            for (int i = 0; i < renderedSlides.Count; i++)
            {
                int slideNumber = i + 1;
                WriteEntry(target, $"ppt/slides/slide{slideNumber}.xml", renderedSlides[i].SlideXml);
                if (!string.IsNullOrWhiteSpace(renderedSlides[i].RelationshipsXml))
                {
                    WriteEntry(target, $"ppt/slides/_rels/slide{slideNumber}.xml.rels", renderedSlides[i].RelationshipsXml!);
                }
            }

            var (presentationRelsXml, slideRelationshipIds) = BuildPowerPointPresentationRelationships(source, renderedSlides.Count);
            WriteEntry(target, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteEntry(target, "ppt/presentation.xml", BuildPowerPointPresentationXml(source, slideRelationshipIds));
            WriteEntry(target, "[Content_Types].xml", BuildPowerPointContentTypesXml(source, renderedSlides.Count));
        }

        return output.ToArray();
    }

    private static IReadOnlyList<string> ExpandPowerPointSlide(string slideXml, string entryName, TemplateRenderContext context)
    {
        var slideScopedMatches = PerTicketBlockRegex.Matches(slideXml)
            .Select(match => ParseDirective(match.Groups["directive"].Value, ".pptx", entryName))
            .Where(directive => directive.Scope == PerTicketLoopScope.Slide)
            .ToList();

        if (slideScopedMatches.Count == 0)
        {
            return [RenderText(slideXml, context, ".pptx", entryName, currentTicketState: null, allowSlideScopedBlocks: false)];
        }

        var primaryDirective = slideScopedMatches[0];
        var tickets = context.Tickets
            .Where(primaryDirective.Matches)
            .ToList();

        if (tickets.Count == 0)
            return [];

        List<string> slides = new(tickets.Count);
        for (int i = 0; i < tickets.Count; i++)
        {
            var ticketState = new TicketRenderState(tickets[i], i);
            slides.Add(RenderText(slideXml, context, ".pptx", entryName, ticketState, allowSlideScopedBlocks: true));
        }

        return slides;
    }

    private static string RenderText(
        string templateContent,
        TemplateRenderContext context,
        string extension,
        string? entryName,
        TicketRenderState? currentTicketState,
        bool allowSlideScopedBlocks)
    {
        var afterBlocks = RenderPerTicketBlocks(templateContent, context, extension, entryName, currentTicketState, allowSlideScopedBlocks);
        return ReplaceSimpleTokens(afterBlocks, context, currentTicketState);
    }

    private static string RenderPerTicketBlocks(
        string templateContent,
        TemplateRenderContext context,
        string extension,
        string? entryName,
        TicketRenderState? currentTicketState,
        bool allowSlideScopedBlocks)
    {
        return PerTicketBlockRegex.Replace(templateContent, match =>
        {
            var directive = ParseDirective(match.Groups["directive"].Value, extension, entryName);
            string body = match.Groups["body"].Value;

            if (directive.Scope == PerTicketLoopScope.Slide)
            {
                if (!allowSlideScopedBlocks)
                    return string.Empty;

                if (currentTicketState is null || !directive.Matches(currentTicketState.Ticket))
                    return string.Empty;

                return RenderText(body, context, extension, entryName, currentTicketState, allowSlideScopedBlocks: false);
            }

            var matchingTickets = context.Tickets
                .Where(directive.Matches)
                .ToList();

            if (matchingTickets.Count == 0)
                return string.Empty;

            var renderedParts = new List<string>(matchingTickets.Count);
            for (int i = 0; i < matchingTickets.Count; i++)
            {
                renderedParts.Add(RenderText(body, context, extension, entryName, new TicketRenderState(matchingTickets[i], i), allowSlideScopedBlocks: false));
            }

            if (directive.Scope == PerTicketLoopScope.Page)
            {
                return string.Join(GetPageSeparator(extension, entryName), renderedParts);
            }

            return string.Concat(renderedParts);
        });
    }

    private static string ReplaceSimpleTokens(string content, TemplateRenderContext context, TicketRenderState? currentTicketState)
    {
        var lookup = CreateLookup(context.GlobalTokens);
        return TokenRegex.Replace(content, match =>
        {
            var key = match.Groups[1].Value;

            if (currentTicketState is not null
                && key.StartsWith("ticket.", StringComparison.OrdinalIgnoreCase)
                && TemplateTicketTokenResolver.TryResolve(currentTicketState.Ticket, currentTicketState.Index, key["ticket.".Length..], out var ticketValue))
            {
                return ticketValue ?? string.Empty;
            }

            return lookup.TryGetValue(key, out var value)
                ? value ?? string.Empty
                : match.Value;
        });
    }

    private static Dictionary<string, string?> CreateLookup(IReadOnlyDictionary<string, string?> tokens)
    {
        var lookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in tokens)
        {
            lookup[kv.Key] = kv.Value;
        }

        return lookup;
    }

    private static string GetPageSeparator(string extension, string? entryName)
    {
        var normalized = ReportTemplateContentTypeMapper.Normalize(extension);

        if (normalized is ".html" or ".htm")
            return Environment.NewLine + "<div style=\"page-break-before:always;\"></div>" + Environment.NewLine;

        if (normalized == ".docx"
            && string.Equals(entryName, "word/document.xml", StringComparison.OrdinalIgnoreCase))
        {
            return "<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>";
        }

        if (normalized == ".txt")
            return Environment.NewLine + "\f" + Environment.NewLine;

        return Environment.NewLine;
    }

    private static PerTicketDirective ParseDirective(string rawDirective, string extension, string? entryName)
    {
        string working = rawDirective?.Trim() ?? string.Empty;
        PerTicketLoopScope scope = PerTicketLoopScope.Section;

        var scopeMatch = ScopeRegex.Match(working);
        if (scopeMatch.Success)
        {
            scope = ParseScope(scopeMatch.Groups[1].Value);
            working = ScopeRegex.Replace(working, string.Empty).Trim();
        }

        // PowerPoint slide XML treats page scope as whole-slide expansion; other formats treat slide as page.
        if (ReportTemplateContentTypeMapper.Normalize(extension) == ".pptx"
            && entryName is not null
            && SlideEntryRegex.IsMatch(entryName)
            && scope == PerTicketLoopScope.Page)
        {
            scope = PerTicketLoopScope.Slide;
        }
        else if (scope == PerTicketLoopScope.Slide)
        {
            scope = PerTicketLoopScope.Page;
        }

        var clauses = SplitClauses(working)
            .Select(ParseClause)
            .Where(clause => clause is not null)
            .Cast<FilterClause>()
            .ToList();

        return new PerTicketDirective(scope, clauses);
    }

    private static PerTicketLoopScope ParseScope(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "page" => PerTicketLoopScope.Page,
            "slide" => PerTicketLoopScope.Slide,
            _ => PerTicketLoopScope.Section
        };
    }

    private static IReadOnlyList<string> SplitClauses(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        List<string> clauses = [];
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < raw.Length; i++)
        {
            char ch = raw[i];
            if (ch == '"' && (i == 0 || raw[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (!inQuotes && ch == ';')
            {
                AddClause(clauses, current);
                continue;
            }

            if (!inQuotes && ch == '&' && i + 1 < raw.Length && raw[i + 1] == '&')
            {
                AddClause(clauses, current);
                i++;
                continue;
            }

            current.Append(ch);
        }

        AddClause(clauses, current);
        return clauses;
    }

    private static void AddClause(ICollection<string> clauses, StringBuilder current)
    {
        string clause = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(clause))
            clauses.Add(clause);

        current.Clear();
    }

    private static FilterClause? ParseClause(string raw)
    {
        var match = ClauseRegex.Match(raw);
        if (!match.Success)
            return null;

        string rawField = match.Groups["field"].Value;
        if (rawField.StartsWith("ticket.", StringComparison.OrdinalIgnoreCase))
            rawField = rawField["ticket.".Length..];

        string field = TemplateTicketTokenResolver.NormalizeFieldLikeToken(rawField);
        string value = Unquote(match.Groups["value"].Value.Trim());
        var op = match.Groups["op"].Value switch
        {
            "=" => FilterOperator.Equals,
            "!=" => FilterOperator.NotEquals,
            "~" => FilterOperator.Contains,
            "!~" => FilterOperator.NotContains,
            _ => FilterOperator.Equals
        };

        return new FilterClause(field, op, value);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
            value = value.Replace("\\\"", "\"");
        }

        return value;
    }

    private static bool ShouldTokenReplace(string entryName)
    {
        var extension = Path.GetExtension(entryName);
        return extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rels", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipPowerPointEntry(string entryName)
    {
        if (entryName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
            || entryName.Equals("ppt/presentation.xml", StringComparison.OrdinalIgnoreCase)
            || entryName.Equals("ppt/_rels/presentation.xml.rels", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SlideEntryRegex.IsMatch(entryName) || SlideRelsEntryRegex.IsMatch(entryName);
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static void WriteEntry(ZipArchive archive, string fullName, string content)
    {
        var entry = archive.CreateEntry(fullName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: false);
        writer.Write(content);
    }

    private static (string Xml, IReadOnlyList<string> SlideRelationshipIds) BuildPowerPointPresentationRelationships(ZipArchive source, int slideCount)
    {
        var relsEntry = source.GetEntry("ppt/_rels/presentation.xml.rels")
            ?? throw new InvalidOperationException("The PowerPoint template is missing ppt/_rels/presentation.xml.rels.");

        XDocument doc = XDocument.Parse(ReadEntryText(relsEntry), LoadOptions.PreserveWhitespace);
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        var root = doc.Root ?? throw new InvalidOperationException("Invalid presentation relationships XML.");

        var relationships = root.Elements(relNs + "Relationship").ToList();
        foreach (var slideRelationship in relationships.Where(e => string.Equals((string?)e.Attribute("Type"), PowerPointSlideRelationshipType, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            slideRelationship.Remove();
        }

        int nextRelationshipNumber = relationships
            .Select(rel => (string?)rel.Attribute("Id"))
            .SelectMany(id => id is null ? Array.Empty<Match>() : RelationshipIdNumberRegex.Matches(id).Cast<Match>())
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        List<string> slideRelationshipIds = new(slideCount);
        for (int i = 0; i < slideCount; i++)
        {
            string relationshipId = $"rId{nextRelationshipNumber + i}";
            slideRelationshipIds.Add(relationshipId);
            root.Add(new XElement(relNs + "Relationship",
                new XAttribute("Id", relationshipId),
                new XAttribute("Type", PowerPointSlideRelationshipType),
                new XAttribute("Target", $"slides/slide{i + 1}.xml")));
        }

        return (SerializeXml(doc), slideRelationshipIds);
    }

    private static string BuildPowerPointPresentationXml(ZipArchive source, IReadOnlyList<string> slideRelationshipIds)
    {
        var presentationEntry = source.GetEntry("ppt/presentation.xml")
            ?? throw new InvalidOperationException("The PowerPoint template is missing ppt/presentation.xml.");

        XDocument doc = XDocument.Parse(ReadEntryText(presentationEntry), LoadOptions.PreserveWhitespace);
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        var root = doc.Root ?? throw new InvalidOperationException("Invalid presentation XML.");
        var slideIdList = root.Element(p + "sldIdLst");
        if (slideIdList is null)
        {
            slideIdList = new XElement(p + "sldIdLst");
            root.Add(slideIdList);
        }

        slideIdList.RemoveNodes();
        int slideId = 256;
        foreach (var relationshipId in slideRelationshipIds)
        {
            slideIdList.Add(new XElement(p + "sldId",
                new XAttribute("id", slideId++),
                new XAttribute(r + "id", relationshipId)));
        }

        return SerializeXml(doc);
    }

    private static string BuildPowerPointContentTypesXml(ZipArchive source, int slideCount)
    {
        var contentTypesEntry = source.GetEntry("[Content_Types].xml")
            ?? throw new InvalidOperationException("The PowerPoint template is missing [Content_Types].xml.");

        XDocument doc = XDocument.Parse(ReadEntryText(contentTypesEntry), LoadOptions.PreserveWhitespace);
        XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        var root = doc.Root ?? throw new InvalidOperationException("Invalid content types XML.");

        var slideOverrides = root.Elements(ct + "Override")
            .Where(e => SlidePartNameRegex.IsMatch((string?)e.Attribute("PartName") ?? string.Empty))
            .ToList();

        string slideContentType = slideOverrides
            .Select(e => (string?)e.Attribute("ContentType"))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
            ?? PowerPointSlideContentType;

        foreach (var slideOverride in slideOverrides)
        {
            slideOverride.Remove();
        }

        for (int i = 0; i < slideCount; i++)
        {
            root.Add(new XElement(ct + "Override",
                new XAttribute("PartName", $"/ppt/slides/slide{i + 1}.xml"),
                new XAttribute("ContentType", slideContentType)));
        }

        return SerializeXml(doc);
    }

    private static string SerializeXml(XDocument document)
    {
        using MemoryStream stream = new();
        using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        }))
        {
            document.Save(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static int ExtractTrailingNumber(string value, Regex regex)
    {
        var match = regex.Match(value);
        return match.Success && int.TryParse(match.Groups["number"].Value, out var parsed) ? parsed : int.MaxValue;
    }

    private sealed record RenderedPowerPointSlide(string SlideXml, string? RelationshipsXml);

    private sealed record TicketRenderState(Ticket Ticket, int Index);

    private sealed record PerTicketDirective(PerTicketLoopScope Scope, IReadOnlyList<FilterClause> Clauses)
    {
        public bool Matches(Ticket ticket)
        {
            if (Clauses.Count == 0)
                return true;

            foreach (var clause in Clauses)
            {
                if (!TemplateTicketTokenResolver.TryResolve(ticket, 0, clause.FieldName, out var value))
                    value = string.Empty;

                value ??= string.Empty;
                bool matches = clause.Operator switch
                {
                    FilterOperator.Equals => string.Equals(value, clause.Value, StringComparison.OrdinalIgnoreCase),
                    FilterOperator.NotEquals => !string.Equals(value, clause.Value, StringComparison.OrdinalIgnoreCase),
                    FilterOperator.Contains => value.Contains(clause.Value, StringComparison.OrdinalIgnoreCase),
                    FilterOperator.NotContains => !value.Contains(clause.Value, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };

                if (!matches)
                    return false;
            }

            return true;
        }
    }

    private sealed record FilterClause(string FieldName, FilterOperator Operator, string Value);

    private enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains
    }

    private enum PerTicketLoopScope
    {
        Section,
        Page,
        Slide
    }
}
