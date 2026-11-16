using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// RFC 6749 section 4.1.2 under a race: a redeem that loses finds the code spent and revokes what the code was spent for.
// The winner's spend and issue are one write transaction, so the loser's revoke can't land between them and leave the
// winner's tokens alive. The issue is slowed (SlowCodeIssue) so the loser always arrives inside that gap. On its own host
// for that reason.
[TestFixture]
public class CodeReplayRaceTests : ApiTestFixture
{
    private HttpClient _anonymous = null!;
    private TokenEndpoint _endpoint = null!;
    private string _clientId = null!;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => SlowCodeIssue.AddTo;

    [OneTimeSetUp]
    public async Task RegisterClient()
    {
        _anonymous = ClientFor(TestUser.Anonymous);
        _endpoint = new TokenEndpoint(Factory, _anonymous);
        _clientId = await OAuthFlow.RegisterAsync(_anonymous, "Claude");
    }

    [OneTimeTearDown]
    public void DisposeAnonymous() => _anonymous.Dispose();

    [Test]
    public async Task Token_WhenCodeRedeemedInParallelThenReplayed_ShouldRevokeEveryTokenIssuedFromIt()
    {
        const int attempts = 5;
        var code = await _endpoint.CodeAsync(_clientId);
        using var start = new Barrier(attempts);

        var responses = await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(TimeSpan.FromSeconds(30)).ShouldBeTrue("every attempt should reach the start line");
                return _endpoint.RedeemAsync(_clientId, code);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap()));

        var issued = new List<(string Access, string Refresh)>();
        foreach (var ok in responses.Where(r => r.StatusCode == HttpStatusCode.OK))
        {
            var body = await TokenEndpoint.OkBodyAsync(ok);
            issued.Add((body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!));
        }
        issued.Count.ShouldBe(1);
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            (await TokenEndpoint.ErrorAsync(refused)).ShouldBe("invalid_grant");
        }
        var (access, refresh) = issued[0];
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized, "the racing replays should revoke the winner's access token");

        (await TokenEndpoint.ErrorAsync(await _endpoint.RedeemAsync(_clientId, code))).ShouldBe("invalid_grant");

        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
        (await TokenEndpoint.ErrorAsync(await _endpoint.RefreshAsync(_clientId, refresh))).ShouldBe("invalid_grant");
    }
}
