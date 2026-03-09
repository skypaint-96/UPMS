namespace UPMS.Api.Security;

using Microsoft.AspNetCore.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = "X-UPMS-API-Key";
    public List<ApiKeyAuthenticationKey> Keys { get; set; } = [];
}

public sealed class ApiKeyAuthenticationKey
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
}
