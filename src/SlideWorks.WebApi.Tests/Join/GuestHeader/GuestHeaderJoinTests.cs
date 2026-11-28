using System.Net;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join.GuestHeader;

// GET /api/join/{code} for the app (K7, D10): with X-SlideWorks-Client: app and no valid guest id, a 200 answers with a new id
// in the X-SlideWorks-Guest response header and sets no cookie; with a valid id, nothing new. A refused code issues nothing,
// and without the client header the web's cookie is unchanged.
[TestFixture]
[CancelAfter(30000)]
public class GuestHeaderJoinTests : JoinFixture
{
    private const string GUEST = "0123456789abcdef0123456789abcdef";

    [Test]
    public async Task Join_WhenAppClientWithoutGuest_ShouldReturnGuestHeaderAndNoCookie()
    {
        Open(CODE, await SharingApi.CreateDeckAsync(Client));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Join(CODE, app: true, guest: null));
        using var malformed = await anonymous.SendAsync(Join(CODE, app: true, guest: "NOT-A-GUEST-ID"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues(GuestCookie.HEADER).Single().ShouldMatch("^[0-9a-f]{32}$");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        malformed.StatusCode.ShouldBe(HttpStatusCode.OK);
        malformed.Headers.GetValues(GuestCookie.HEADER).Single().ShouldMatch("^[0-9a-f]{32}$");
        malformed.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task Join_WhenAppClientWithGuest_ShouldNotIssueAnother()
    {
        Open(CODE, await SharingApi.CreateDeckAsync(Client));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Join(CODE, app: true, guest: GUEST));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains(GuestCookie.HEADER).ShouldBeFalse();
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task Join_WhenRefused_ShouldIssueNothing()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Join("111111", app: true, guest: null));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"no_session"}""");
        response.Headers.Contains(GuestCookie.HEADER).ShouldBeFalse();
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task Join_WhenTooManyAttempts_ShouldIssueNothing()
    {
        Open(CODE, await SharingApi.CreateDeckAsync(Client));
        using var anonymous = ClientFor(TestUser.Anonymous);
        Limiter.Refuse = true;
        try
        {
            using var response = await anonymous.SendAsync(Join(CODE, app: true, guest: null));

            response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            (await response.Content.ReadAsStringAsync()).ShouldContain("\"error\":\"too_many_attempts\"", Case.Sensitive);
            response.Headers.Contains(GuestCookie.HEADER).ShouldBeFalse();
            response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        }
        finally
        {
            Limiter.Refuse = false;
        }
    }

    [Test]
    public async Task Join_WhenWebClient_ShouldSetCookieOnly()
    {
        Open(CODE, await SharingApi.CreateDeckAsync(Client));
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.SendAsync(Join(CODE, app: false, guest: null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie").Single().ShouldMatch("^sw_guest=[0-9a-f]{32}; ");
        response.Headers.Contains(GuestCookie.HEADER).ShouldBeFalse();
    }

    private static HttpRequestMessage Join(string code, bool app, string? guest)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{code}");
        if (app)
        {
            request.Headers.Add(GuestCookie.CLIENT_HEADER, GuestCookie.APP_CLIENT);
        }
        if (guest is not null)
        {
            request.Headers.Add(GuestCookie.HEADER, guest);
        }
        return request;
    }
}
