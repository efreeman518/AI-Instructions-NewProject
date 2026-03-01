using System.Net.Http.Headers;
using Uno.Extensions.Authentication;

namespace TaskFlow.UI.Infrastructure;

/// <summary>
/// DelegatingHandler that attaches Bearer tokens to outgoing Gateway requests.
/// Reads the cached access token from ITokenCache (populated by UseAuthentication).
///
/// This handler works with both the dev-mode custom auth provider and production
/// MSAL authentication — no changes needed when upgrading to Entra External ID.
/// The token is stored under the "AccessToken" key in the token cache by either provider.
/// </summary>
public class AuthTokenHandler : DelegatingHandler
{
    private readonly ITokenCache _tokenCache;

    public AuthTokenHandler(ITokenCache tokenCache)
    {
        _tokenCache = tokenCache;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await _tokenCache.GetAsync(cancellationToken);
        if (tokens is not null && tokens.TryGetValue("AccessToken", out var accessToken)
            && !string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
