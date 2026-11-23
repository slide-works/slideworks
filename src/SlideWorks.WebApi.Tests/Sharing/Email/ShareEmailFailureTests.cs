using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Email;
using SlideWorks.WebApi.Infrastructure.Mail;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// A relay that refuses every message must not touch the share, and the failure is logged without the message. The queue is
// in order, so once the barrier address (the one address the relay accepts) is mailed, the refused send has been logged.
[TestFixture]
public class ShareEmailFailureTests : ApiTestFixture
{
    private const string BARRIER_PREFIX = "barrier-";

    private CapturingLoggerProvider _log = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        _log = CapturingLoggerProvider.Replace<ShareEmailWorker>(services);
        services.AddSingleton<RefusingMailSender>().AddSingleton<IMailSender>(p => p.GetRequiredService<RefusingMailSender>());
    };

    [Test]
    public async Task Send_WhenSmtpFails_ShouldLogAndKeepShare()
    {
        var recipient = $"r-{Guid.NewGuid():N}@recipient.example";
        var deck = await NewDeckAsync("Secret roadmap");

        var shared = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = new[] { recipient } });
        var barrier = await NewDeckAsync("Barrier");
        await SharingApi.PutOkAsync(Client, barrier, "named", $"{BARRIER_PREFIX}{Guid.NewGuid():N}@recipient.example");
        await Factory.Services.GetRequiredService<RefusingMailSender>().Accepted.Task;

        shared.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sharing = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        sharing.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe([recipient]);
        var lines = string.Join("\n", _log.Lines);
        lines.ShouldContain("was not sent");
        lines.ShouldNotContain("Secret roadmap");
        lines.ShouldNotContain(recipient);
    }

    private async Task<string> NewDeckAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    public sealed class RefusingMailSender : IMailSender
    {
        public TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default)
        {
            if (message.To.StartsWith(BARRIER_PREFIX, StringComparison.Ordinal))
            {
                Accepted.TrySetResult();
                return Task.CompletedTask;
            }
            throw new InvalidOperationException("relay refused the message");
        }
    }
}
