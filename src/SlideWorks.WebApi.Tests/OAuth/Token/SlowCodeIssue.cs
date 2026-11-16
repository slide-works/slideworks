using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// Wraps the registered IAgentTokens and waits before each code's tokens are issued, so a redeem racing the winner reliably
// arrives between the winner's spend and its issue. Timing only: every call still goes to the real store.
public sealed class SlowCodeIssue(IAgentTokens inner) : IAgentTokens
{
    public static readonly TimeSpan DELAY = TimeSpan.FromMilliseconds(500);

    public static void AddTo(IServiceCollection services)
    {
        var registered = services.Last(s => s.ServiceType == typeof(IAgentTokens));
        var implementation = registered.ImplementationType ?? throw new InvalidOperationException("IAgentTokens should be registered by type.");
        services.Add(new ServiceDescriptor(implementation, implementation, registered.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(IAgentTokens),
            provider => new SlowCodeIssue((IAgentTokens)provider.GetRequiredService(implementation)),
            registered.Lifetime));
    }

    public Task<AgentTokenPair> IssueAsync(CodeGrant grant) => inner.IssueAsync(grant);

    public async Task<AgentTokenPair> IssueForCodeAsync(string code, CodeGrant grant)
    {
        await Task.Delay(DELAY);
        return await inner.IssueForCodeAsync(code, grant);
    }

    public Task RevokeIssuedForCodeAsync(string code) => inner.RevokeIssuedForCodeAsync(code);

    public Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId) => inner.RefreshAsync(refreshToken, clientId);

    public Task<AgentPrincipal?> ValidateAsync(string accessToken, string resource) => inner.ValidateAsync(accessToken, resource);

    public Task RevokeAsync(string token) => inner.RevokeAsync(token);

    public Task RevokeAllForPresenterAsync(Guid presenterId) => inner.RevokeAllForPresenterAsync(presenterId);
}
