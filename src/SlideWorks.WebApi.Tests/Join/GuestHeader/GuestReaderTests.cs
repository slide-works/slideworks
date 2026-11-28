using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SlideWorks.WebApi.Features.Join;

namespace SlideWorks.WebApi.Tests.Join.GuestHeader;

// The one guest reader (K7): the sw_guest cookie when valid, else the X-SlideWorks-Guest header, else on /hubs only the guest
// query. Anything that isn't 32 lowercase hex digits counts as no guest.
[TestFixture]
public class GuestReaderTests
{
    private const string COOKIE_ID = "0123456789abcdef0123456789abcdef";
    private const string HEADER_ID = "fedcba9876543210fedcba9876543210";

    [Test]
    public void GuestReader_WhenCookieAndHeaderBothPresent_ShouldPreferCookie()
    {
        var both = Request("/api/join/482913/poll", cookie: COOKIE_ID, header: HEADER_ID);
        var malformedCookie = Request("/api/join/482913/poll", cookie: "not-a-guest-id", header: HEADER_ID);
        var headerOnly = Request("/api/join/482913/poll", header: HEADER_ID);

        GuestCookie.From(both).ShouldBe(COOKIE_ID);
        GuestCookie.From(malformedCookie).ShouldBe(HEADER_ID);
        GuestCookie.From(headerOnly).ShouldBe(HEADER_ID);
    }

    [Test]
    public void GuestReader_WhenHeaderMalformed_ShouldTreatAsNoGuest()
    {
        var upper = Request("/api/join/482913/poll", header: HEADER_ID.ToUpperInvariant());
        var tooShort = Request("/api/join/482913/poll", header: HEADER_ID[..31]);
        var notHex = Request("/api/join/482913/poll", header: "not-a-guest-id");
        var twice = Request("/api/join/482913/poll", header: HEADER_ID);
        twice.Headers.Append(GuestCookie.HEADER, COOKIE_ID);

        GuestCookie.From(upper).ShouldBeNull();
        GuestCookie.From(tooShort).ShouldBeNull();
        GuestCookie.From(notHex).ShouldBeNull();
        GuestCookie.From(twice).ShouldBeNull();
    }

    [Test]
    public void GuestReader_WhenGuestQueryOnApi_ShouldIgnoreIt()
    {
        var api = Request("/api/join/482913/poll", query: HEADER_ID);
        var hub = Request("/hubs/deck", query: HEADER_ID);
        var negotiate = Request("/hubs/deck/negotiate", query: HEADER_ID);
        var malformedOnHub = Request("/hubs/deck", query: "not-a-guest-id");

        GuestCookie.From(api).ShouldBeNull();
        GuestCookie.From(hub).ShouldBe(HEADER_ID);
        GuestCookie.From(negotiate).ShouldBe(HEADER_ID);
        GuestCookie.From(malformedOnHub).ShouldBeNull();
    }

    private static HttpRequest Request(string path, string? cookie = null, string? header = null, string? query = null)
    {
        var request = new DefaultHttpContext().Request;
        request.Path = path;
        if (cookie is not null)
        {
            request.Headers.Cookie = $"{GuestCookie.NAME}={cookie}";
        }
        if (header is not null)
        {
            request.Headers[GuestCookie.HEADER] = header;
        }
        if (query is not null)
        {
            request.Query = new QueryCollection(new Dictionary<string, StringValues> { [GuestCookie.QUERY] = query });
        }
        return request;
    }
}
