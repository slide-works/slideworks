using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// An agent connecting over OAuth, in the browser: it registers over HTTP, sends the presenter to /oauth/authorize, and
// the consent page sends the browser back to the agent's loopback callback with a code or access_denied. The callback is
// answered by page.RouteAsync, as the agent's local listener would. A signed-out browser goes to sign in first.
public partial class AgentConsentJourneys : AcceptanceTest
{
    private const string CALLBACK = "http://127.0.0.1:33418/callback";
    private const string STATE = "st8";

    // RFC 7636 appendix B's example challenge.
    private const string CHALLENGE = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Test]
    public async Task Consent_WhenPresenterAllows_ShouldReturnToTheAgentWithCodeAndState()
    {
        var authorize = await StartAsync(TestUser.Sam);
        var consent = new ConsentPage(Page);

        await Page.GotoAsync(authorize);
        await consent.ShouldAskForAsync("Claude");
        await Expect(consent.ReturnsTo).ToHaveTextAsync("127.0.0.1:33418");
        await consent.AllowAsync();

        await Expect(Page).ToHaveURLAsync(CodeCallback());
    }

    [Test]
    public async Task Consent_WhenPresenterDenies_ShouldReturnToTheAgentWithAccessDenied()
    {
        var authorize = await StartAsync(TestUser.Sam);
        var consent = new ConsentPage(Page);

        await Page.GotoAsync(authorize);
        await consent.ShouldAskForAsync("Claude");
        await consent.DenyAsync();

        await Expect(Page).ToHaveURLAsync($"{CALLBACK}?error=access_denied&state={STATE}");
    }

    [Test]
    public async Task Authorize_WhenSignedOut_ShouldSendTheBrowserToSignInAndKeepTheFullQuery()
    {
        var authorize = await StartAsync(presenter: null);

        await Page.GotoAsync(authorize);

        await new SignInPage(Page).ShouldBeShownWithReturnToAsync(authorize);
    }

    // Registers a client over HTTP as an agent would, answers its loopback callback, signs the presenter in (or leaves
    // the browser signed out), and returns the /oauth/authorize path the agent opens.
    private async Task<string> StartAsync(TestUser? presenter)
    {
        var registered = await Page.APIRequest.PostAsync(
            AcceptanceApp.BaseUrl + "/oauth/register",
            new() { DataObject = new { client_name = "Claude", redirect_uris = new[] { CALLBACK } } });
        registered.Status.ShouldBe(201);
        var clientId = (await registered.JsonAsync()).ShouldNotBeNull().GetProperty("client_id").GetString().ShouldNotBeNull();
        await Page.RouteAsync(CALLBACK + "**", route => route.FulfillAsync(new() { ContentType = "text/plain", Body = "Agent got the answer." }));
        if (presenter != null)
        {
            await SignIn.AsAsync(Page, presenter);
        }
        return "/oauth/authorize?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(clientId)
            + "&redirect_uri=" + Uri.EscapeDataString(CALLBACK)
            + "&state=" + STATE
            + "&code_challenge=" + CHALLENGE
            + "&code_challenge_method=S256"
            + "&scope=decks"
            + "&resource=" + Uri.EscapeDataString(AcceptanceApp.BaseUrl + "/mcp");
    }

    [GeneratedRegex(@"^http://127\.0\.0\.1:33418/callback\?code=[A-Za-z0-9_-]{43}&state=st8$")]
    private static partial Regex CodeCallback();
}
