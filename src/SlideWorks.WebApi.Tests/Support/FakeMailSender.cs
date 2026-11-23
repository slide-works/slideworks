using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Mail;

namespace SlideWorks.WebApi.Tests.Support;

// IMailSender for tests of what sends mail: it sends nothing and records every message. The app, and so this fake, is shared
// by every test in a fixture: each test uses recipient addresses of its own and reads SentTo its own. Add it with AddTo.
public sealed class FakeMailSender : IMailSender
{
    private readonly Lock _gate = new();
    private readonly List<MailMessageModel> _sent = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeMailSender>().AddSingleton<IMailSender>(provider => provider.GetRequiredService<FakeMailSender>());

    // The messages sent to this address, in order.
    public IReadOnlyList<MailMessageModel> SentTo(string address)
    {
        lock (_gate)
        {
            return [.. _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase))];
        }
    }

    public Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sent.Add(message);
        }
        return Task.CompletedTask;
    }
}
