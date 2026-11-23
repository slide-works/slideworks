using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Mail;

using SlideWorks.WebApi.Tests.Sharing;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// No SLIDEWORKS_SMTP_HOST: the app's own IMailSender is NoMailSender, sharing works, and no email is recorded as sent.
[TestFixture]
public class ShareEmailNoSmtpTests : ApiTestFixture
{
    [Test]
    public async Task Share_WhenNoSmtpHost_ShouldStillShareAndSendNothing()
    {
        var recipient = $"r-{Guid.NewGuid():N}@recipient.example";
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Plain" });
        var deck = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var shared = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = new[] { recipient } });

        shared.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Services.GetRequiredService<IMailSender>().ShouldBeOfType<NoMailSender>();
        var sharing = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        sharing.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe([recipient]);
    }
}
