using Lugat.Application.Users;
using Lugat.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lugat.Api.Auth;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    /// <summary>OAuth client id of the web app (Google Cloud console). Public; it is the token's expected audience.</summary>
    public string ClientId { get; set; } = "";
}

/// <summary>Google's current token signing keys.</summary>
public interface IGoogleSigningKeys
{
    /// <param name="refresh">Fetch again even if cached: a token named a key the cache does not have (Google rotated).</param>
    Task<IReadOnlyList<SecurityKey>> GetAsync(bool refresh, CancellationToken ct);
}

/// <summary>Reads Google's published JWKS and keeps it for as long as Google's Cache-Control allows (max 1 day).</summary>
public sealed class GoogleJwksSigningKeys(HttpClient http, TimeProvider time) : IGoogleSigningKeys
{
    public static readonly Uri JwksUri = new("https://www.googleapis.com/oauth2/v3/certs");

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<SecurityKey> _keys = [];
    private DateTimeOffset _expires;

    public async Task<IReadOnlyList<SecurityKey>> GetAsync(bool refresh, CancellationToken ct)
    {
        if (!refresh && _keys.Count > 0 && time.GetUtcNow() < _expires)
        {
            return _keys;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (!refresh && _keys.Count > 0 && time.GetUtcNow() < _expires)
            {
                return _keys;
            }

            using var response = await http.GetAsync(JwksUri, ct);
            response.EnsureSuccessStatusCode();
            var set = new JsonWebKeySet(await response.Content.ReadAsStringAsync(ct));
            var maxAge = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromHours(1);
            _keys = [.. set.GetSigningKeys()];
            _expires = time.GetUtcNow() + (maxAge > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : maxAge);
            return _keys;
        }
        finally
        {
            _gate.Release();
        }
    }
}

public enum GoogleLoginError
{
    None,
    NotConfigured,
    Invalid,
    Unavailable,
}

public sealed record GoogleLoginResult(VerifiedLogin? Login, GoogleLoginError Error);

/// <summary>
/// Checks a Google Identity Services ID token on the server (spec §6.3): RS256 signature by one of Google's keys,
/// <c>aud</c> = our client id, <c>iss</c> = accounts.google.com, not expired. The identity is <c>sub</c>, which
/// never changes; the email is only shown, and only when Google says it is verified.
/// </summary>
public sealed class GoogleIdTokenValidator(IOptionsMonitor<GoogleOptions> options, IGoogleSigningKeys keys, TimeProvider time, ILogger<GoogleIdTokenValidator> logger)
{
    public static readonly string[] Issuers = ["accounts.google.com", "https://accounts.google.com"];

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    private readonly JsonWebTokenHandler _handler = new();

    public async Task<GoogleLoginResult> ValidateAsync(string? idToken, CancellationToken ct)
    {
        var clientId = options.CurrentValue.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new(null, GoogleLoginError.NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 8192)
        {
            return new(null, GoogleLoginError.Invalid);
        }

        TokenValidationResult result;
        try
        {
            result = await ValidateWithKeysAsync(idToken, clientId, await keys.GetAsync(refresh: false, ct));
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                result = await ValidateWithKeysAsync(idToken, clientId, await keys.GetAsync(refresh: true, ct));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Google signing keys could not be loaded: {Error}", ex.GetType().Name);
            return new(null, GoogleLoginError.Unavailable);
        }

        if (!result.IsValid)
        {
            logger.LogInformation("Google ID token rejected: {Error}", result.Exception?.GetType().Name);
            return new(null, GoogleLoginError.Invalid);
        }

        var claims = result.ClaimsIdentity;
        var subject = claims.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject) || subject.Length > 128)
        {
            return new(null, GoogleLoginError.Invalid);
        }

        var emailVerified = claims.FindFirst("email_verified")?.Value is "true" or "True";
        return new(new VerifiedLogin(
            LoginProvider.Google,
            subject,
            claims.FindFirst("name")?.Value,
            emailVerified ? claims.FindFirst("email")?.Value : null), GoogleLoginError.None);
    }

    private Task<TokenValidationResult> ValidateWithKeysAsync(string token, string clientId, IReadOnlyList<SecurityKey> signingKeys) =>
        _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuers = Issuers,
            ValidAudience = clientId,
            IssuerSigningKeys = signingKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            // The app's clock (fake in tests), not the machine's.
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = time.GetUtcNow().UtcDateTime;
                return expires is { } exp && now <= exp + ClockSkew && (notBefore is not { } nbf || now >= nbf - ClockSkew);
            },
        });
}
