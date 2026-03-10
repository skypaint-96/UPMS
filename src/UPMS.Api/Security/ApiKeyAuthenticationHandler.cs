namespace UPMS.Api.Security;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        string suppliedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(suppliedKey))
            return Task.FromResult(AuthenticateResult.Fail("API key header was empty."));

        var matchedKey = Options.Keys.FirstOrDefault(key => ConstantTimeEquals(key.Value, suppliedKey));
        if (matchedKey is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, matchedKey.Name),
            new(ClaimTypes.NameIdentifier, matchedKey.Name),
            new("auth_scheme", ApiKeyAuthenticationDefaults.SchemeName)
        };

        foreach (var role in matchedKey.Roles.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool ConstantTimeEquals(string expected, string supplied)
    {
        var left = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        var right = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
