namespace UPMS.Web.Services;

public interface ICsvTemplateService
{
    Task<byte[]?> GetTemplateBytesAsync(string itsmSourceName, bool requiredOnly);
}
