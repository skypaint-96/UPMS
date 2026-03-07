namespace UPMS.Web.Templates;

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

public static class TemplateTokenRenderer
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled);

    public static string RenderText(string templateContent, IReadOnlyDictionary<string, string?> tokens)
    {
        var lookup = CreateLookup(tokens);
        return TokenRegex.Replace(templateContent, match =>
        {
            var key = match.Groups[1].Value;
            return lookup.TryGetValue(key, out var value)
                ? value ?? string.Empty
                : match.Value;
        });
    }

    public static byte[] RenderBytes(byte[] templateContent, string extension, IReadOnlyDictionary<string, string?> tokens)
    {
        var normalized = ReportTemplateContentTypeMapper.Normalize(extension);
        if (ReportTemplateContentTypeMapper.IsTextLike(normalized))
        {
            var text = Encoding.UTF8.GetString(templateContent);
            return Encoding.UTF8.GetBytes(RenderText(text, tokens));
        }

        if (ReportTemplateContentTypeMapper.IsOoxmlPackage(normalized))
        {
            return RenderOoxmlPackage(templateContent, tokens);
        }

        return templateContent;
    }

    private static byte[] RenderOoxmlPackage(byte[] templateContent, IReadOnlyDictionary<string, string?> tokens)
    {
        var lookup = CreateLookup(tokens);

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
                    var rendered = TokenRegex.Replace(text, match =>
                    {
                        var key = match.Groups[1].Value;
                        return lookup.TryGetValue(key, out var value)
                            ? value ?? string.Empty
                            : match.Value;
                    });

                    using StreamWriter writer = new(entryOutput, Encoding.UTF8, leaveOpen: true);
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

    private static Dictionary<string, string?> CreateLookup(IReadOnlyDictionary<string, string?> tokens)
    {
        var lookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in tokens)
        {
            lookup[kv.Key] = kv.Value;
        }

        return lookup;
    }

    private static bool ShouldTokenReplace(string entryName)
    {
        var extension = Path.GetExtension(entryName);
        return extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rels", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }
}
