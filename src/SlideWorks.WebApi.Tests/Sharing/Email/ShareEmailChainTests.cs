using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Mail;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// The chain a person follows: a share email arrives, they open its unsubscribe link and press the button, and the next
// share to that address sends nothing. The token comes from the mail itself; it is never built here.
[TestFixture]
public class ShareEmailChainTests : ApiTestFixture
{
    private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(20);

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeMailSender.AddTo(services);
        services.AddSingleton<IMailSender>(provider => new SignallingMailSender(provider.GetRequiredService<FakeMailSender>()));
    };

    [Test]
    public async Task Unsubscribe_WhenLinkFromShareEmailUsed_ShouldStopNextEmail()
    {
        var recipient = NewAddress();
        var barrier = NewAddress();
        var first = await NewDeckIdAsync("Chain one");
        var second = await NewDeckIdAsync("Chain two");
        var third = await NewDeckIdAsync("Chain three");
        await SharingApi.PutOkAsync(Client, first, "named", recipient);
        await WaitForMailAsync(recipient, 1);
        var url = Mail.SentTo(recipient)[0].UnsubscribeUrl.ShouldNotBeNull();
        var token = url[(url.IndexOf("#t=", StringComparison.Ordinal) + 3)..];

        using var anonymous = ClientFor(TestUser.Anonymous);
        var response = await anonymous.PostAsJsonAsync("/api/email/unsubscribe", new { token });
        await SharingApi.PutOkAsync(Client, second, "named", recipient);
        // A barrier address on the same in-order queue: once its mail is recorded, the second share has been handled.
        await SharingApi.PutOkAsync(Client, third, "named", barrier);
        await WaitForMailAsync(barrier, 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Mail.SentTo(recipient).Count.ShouldBe(1);
    }

    private FakeMailSender Mail => Factory.Services.GetRequiredService<FakeMailSender>();

    private static string NewAddress() => $"r-{Guid.NewGuid():N}@recipient.example";

    private async Task<string> NewDeckIdAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private async Task WaitForMailAsync(string address, int count)
    {
        var sender = (SignallingMailSender)Factory.Services.GetRequiredService<IMailSender>();
        await sender.WaitUntilAsync(() => Mail.SentTo(address).Count >= count, WAIT);
        Mail.SentTo(address).Count.ShouldBe(count);
    }
}
