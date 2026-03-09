namespace UPMS.Reporting.Templates;

public interface IReportTemplateBootstrapper
{
    Task EnsureSeededAsync(CancellationToken ct = default);
}
