using System.Net;
using System.Net.Http.Json;
using SlideWorks.WebApi.Features.OAuth.Registration;
using SlideWorks.WebApi.Features.OAuth.Token;
using SlideWorks.WebApi.Tests.OAuth.Authorize;

namespace SlideWorks.WebApi.Tests.RateLimits;

// Calls a rate-limited endpoint as a caller at a given remote address (TestRemoteAddress), optionally with an
// X-Forwarded-For header, the way a client or a proxy in front of SlideWorks would. A Token call is a revoke of an unknown
// token (200, and it counts against the token budget); a Registration call registers a client (201).
public sealed class RateLimitProbe(HttpClient anonymous)
{
    public static int BudgetOf(LimitedEndpoint endpoint)
        => endpoint == LimitedEndpoint.Token ? TokenLimiter.PER_MINUTE : RegistrationLimiter.PER_MINUTE;

    public async Task<HttpStatusCode> SendAsync(LimitedEndpoint endpoint, string? remote, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint == LimitedEndpoint.Token ? "/oauth/revoke" : "/oauth/register")
        {
            Content = endpoint == LimitedEndpoint.Token
                ? new FormUrlEncodedContent([KeyValuePair.Create("token", "not-a-token")])
                : JsonContent.Create(new { client_name = "Claude", redirect_uris = new[] { OAuthFlow.REDIRECT } }),
        };
        if (remote != null)
        {
            request.Headers.Add(TestRemoteAddress.HEADER, remote);
        }
        if (forwardedFor != null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }
        using var response = await anonymous.SendAsync(request);
        return response.StatusCode;
    }

    public static HttpStatusCode Served(LimitedEndpoint endpoint)
        => endpoint == LimitedEndpoint.Token ? HttpStatusCode.OK : HttpStatusCode.Created;

    // What count calls charged to one caller get: the first budget-many are served, every one after them is 429.
    public static IReadOnlyList<HttpStatusCode> BudgetThenRefused(LimitedEndpoint endpoint, int count)
        => Enumerable.Range(0, count).Select(i => i < BudgetOf(endpoint) ? Served(endpoint) : HttpStatusCode.TooManyRequests).ToList();

    // One call per caller, in order; the status of each.
    public async Task<IReadOnlyList<HttpStatusCode>> SendAllAsync(LimitedEndpoint endpoint, IReadOnlyList<(string? Remote, string? ForwardedFor)> callers)
    {
        var statuses = new List<HttpStatusCode>();
        foreach (var (remote, forwardedFor) in callers)
        {
            statuses.Add(await SendAsync(endpoint, remote, forwardedFor));
        }
        return statuses;
    }
}
