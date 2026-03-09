namespace UPMS.Reporting.Templates;

using System.Reflection;

public sealed class ReportTemplateBootstrapper : IReportTemplateBootstrapper
{
    private readonly IReportTemplateStore _templateStore;
    private readonly ReportTemplateTypeRegistry _typeRegistry;

    public ReportTemplateBootstrapper(IReportTemplateStore templateStore, ReportTemplateTypeRegistry typeRegistry)
    {
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
    }

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        var assembly = typeof(ReportTemplateBootstrapper).Assembly;
        var existing = _templateStore.GetAllTemplates();

        foreach (var definition in _typeRegistry.GetAll())
        {
            if (string.IsNullOrWhiteSpace(definition.StarterTemplateResourceName))
                continue;

            if (existing.Any(template => template.IsStarterTemplate
                && string.Equals(template.TemplateTypeId, definition.TypeId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(definition.StarterTemplateResourceName!);
            if (stream is null)
                throw new InvalidOperationException($"Starter report template resource '{definition.StarterTemplateResourceName}' could not be found.");

            try
            {
                await _templateStore.SaveAsync(new ReportTemplateUploadRequest
                {
                    TemplateId = $"starter-{definition.TypeId}",
                    TemplateTypeId = definition.TypeId,
                    DisplayName = definition.StarterTemplateDisplayName ?? definition.DisplayName,
                    Kind = definition.Kind,
                    Description = definition.StarterTemplateDescription ?? definition.Description,
                    SubjectTemplate = definition.DefaultSubjectTemplate,
                    OriginalFileName = $"starter{definition.PrimaryExtension}",
                    UploadedBy = "system",
                    IsStarterTemplate = true
                }, stream, ct);
            }
            catch (InvalidOperationException)
            {
                // Another process may have seeded the same starter template already.
            }
            catch (IOException)
            {
                // Another process may have seeded the same starter template already.
            }
        }
    }
}
