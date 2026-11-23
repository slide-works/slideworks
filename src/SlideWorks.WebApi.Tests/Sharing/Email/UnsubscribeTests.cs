using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Email;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// The unsubscribe link and the one call its page makes. The token is the credential, so the route is anonymous.
[TestFixture]
public class UnsubscribeTests : ApiTestFixture
{
    [Test]
    public async Task Unsubscribe_WhenValidToken_ShouldSuppressAddress()
    {
        var address = NewAddress();
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsJsonAsync("/api/email/unsubscribe", new { token = TokenFor(address) });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsSuppressedAsync(address)).ShouldBeTrue();
        (await IsSuppressedAsync(NewAddress())).ShouldBeFalse();
    }

    [Test]
    public async Task Unsubscribe_WhenTamperedToken_ShouldReturn400AndWriteNothing()
    {
        var address = NewAddress();
        var token = TokenFor(address);
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        using var anonymous = ClientFor(TestUser.Anonymous);

        foreach (var bad in new[] { tampered, "not-a-token", "stand-in", token[..10] })
        {
            var response = await anonymous.PostAsJsonAsync("/api/email/unsubscribe", new { token = bad });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).ShouldContain("error");
        }
        (await IsSuppressedAsync(address)).ShouldBeFalse();
    }

    [Test]
    public async Task Unsubscribe_WhenTwice_ShouldReturn204()
    {
        var address = NewAddress();
        using var anonymous = ClientFor(TestUser.Anonymous);

        var first = await anonymous.PostAsJsonAsync("/api/email/unsubscribe", new { token = TokenFor(address) });
        var second = await anonymous.PostAsJsonAsync("/api/email/unsubscribe", new { token = TokenFor(address.ToUpperInvariant()) });

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsSuppressedAsync(address)).ShouldBeTrue();
    }

    [Test]
    public void For_WhenEmailGiven_ShouldPointAtPublicUrlFragment()
    {
        using var scope = Factory.Services.CreateScope();

        var link = scope.ServiceProvider.GetRequiredService<IUnsubscribeLinks>().For(NewAddress());

        link.ShouldStartWith(PublicUrl + "/unsubscribe#t=");
        link.ShouldNotContain("?");
        link[(link.IndexOf("#t=", StringComparison.Ordinal) + 3)..].ShouldMatch("^[A-Za-z0-9_-]+$");
    }

    private static string NewAddress() => $"u-{Guid.NewGuid():N}@recipient.example";

    private string TokenFor(string address)
    {
        using var scope = Factory.Services.CreateScope();
        var link = scope.ServiceProvider.GetRequiredService<IUnsubscribeLinks>().For(address);
        return link[(link.IndexOf("#t=", StringComparison.Ordinal) + 3)..];
    }

    private async Task<bool> IsSuppressedAsync(string address)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailSuppressions>().ContainsAsync(address);
    }
}
