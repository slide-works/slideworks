using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// The K10 contract tests run against whatever store the app registers, resolved from the app's own container. Each call
// gets its own scope, as a request would, so a per-request EF store passes through here unchanged.
public abstract class OAuthContractFixture : ApiTestFixture
{
    protected const string SCOPE = "decks";
    protected const string REDIRECT = "http://127.0.0.1:33418/callback";
    protected const string OTHER_RESOURCE = "https://elsewhere.example/mcp";

    // RFC 7636 appendix B's example pair, so the S256 check is pinned to the spec, not to our own helper.
    protected const string VERIFIER = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    protected const string CHALLENGE = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    protected const int PARALLEL_ATTEMPTS = 20;

    // The one resource: {public}/mcp, which is http://localhost/mcp on the test host.
    protected string Resource => Factory.Services.GetRequiredService<IPublicUrls>().Mcp;

    protected async Task<TResult> UseAsync<TStore, TResult>(Func<TStore, Task<TResult>> call)
        where TStore : notnull
    {
        using var scope = Factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<TStore>());
    }

    protected async Task UseAsync<TStore>(Func<TStore, Task> call)
        where TStore : notnull
    {
        using var scope = Factory.Services.CreateScope();
        await call(scope.ServiceProvider.GetRequiredService<TStore>());
    }

    // The person signs in through TestPeople, as every test seam does, so Sam, Alex and Mira are approved.
    protected async Task<Guid> PresenterAsync(TestUser user)
    {
        using var scope = Factory.Services.CreateScope();
        return (await TestPeople.SignInAsync(scope.ServiceProvider, user)).Id;
    }

    protected Task<OAuthClient> RegisterClientAsync(string name = "Claude")
        => UseAsync<IOAuthClients, OAuthClient>(c => c.RegisterAsync(new ClientRegistration(name, [REDIRECT])));

    // A live token family for a registered client acting for Sam, issued as POST /oauth/token would after a code.
    protected async Task<(OAuthClient Client, Guid PresenterId, AgentTokenPair Tokens)> IssueTokensAsync(string clientName = "Claude")
    {
        var presenterId = await PresenterAsync(TestUser.Sam);
        var client = await RegisterClientAsync(clientName);
        var tokens = await UseAsync<IAgentTokens, AgentTokenPair>(t => t.IssueAsync(new CodeGrant(client.ClientId, presenterId, SCOPE, Resource)));
        return (client, presenterId, tokens);
    }

    // Runs every attempt on its own thread, each with its own scope (as parallel requests would have), released together
    // by a barrier, so they really race. Thread-pool tasks would start in waves and could miss each other.
    protected async Task<TResult[]> RaceAsync<TStore, TResult>(Func<TStore, Task<TResult>> attempt)
        where TStore : notnull
    {
        var scopes = Enumerable.Range(0, PARALLEL_ATTEMPTS).Select(_ => Factory.Services.CreateScope()).ToList();
        try
        {
            using var start = new Barrier(PARALLEL_ATTEMPTS);
            var attempts = scopes.Select(scope =>
            {
                var store = scope.ServiceProvider.GetRequiredService<TStore>();
                return Task.Factory.StartNew(
                    () =>
                    {
                        start.SignalAndWait(TimeSpan.FromSeconds(30)).ShouldBeTrue("every attempt should reach the start line");
                        return attempt(store);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();
            }).ToList();
            return await Task.WhenAll(attempts);
        }
        finally
        {
            scopes.ForEach(scope => scope.Dispose());
        }
    }

    protected static string S256(string verifier)
        => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
}
