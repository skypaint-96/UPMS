using System.Text;
using UPMS.Data;

namespace UPMS.Web.Services;

public class CsvTemplateService : ICsvTemplateService
{
    private readonly IItsmSourceService _sourceService;

    public CsvTemplateService(IItsmSourceService sourceService)
    {
        _sourceService = sourceService ?? throw new ArgumentNullException(nameof(sourceService));
    }

    public async Task<byte[]?> GetTemplateBytesAsync(string itsmSourceName, bool requiredOnly)
    {
        if (string.IsNullOrWhiteSpace(itsmSourceName)) return null;

        ItsmSourceDefinition? def;
        try
        {
            def = await _sourceService.GetSourceDefinitionAsync(itsmSourceName);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }

        if (def is null) return null;

        var mappings = def.Mappings;
        if (requiredOnly)
        {
            mappings = mappings.Where(m => m.IsRequired).ToList();
        }

        var headers = mappings.Select(m => m.SourceFieldName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var headerLine = string.Join(',', headers);
        var bytes = Encoding.UTF8.GetBytes(headerLine + "\r\n");
        return bytes;
    }
}
