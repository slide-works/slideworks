using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// TODO: build the share email, check suppressions and the per-recipient limit, and send it on a background queue.
[RegisterPerRequest]
public sealed class ShareNotifier : IShareNotifier
{
    public Task QueueAsync(ShareNotice notice) => Task.CompletedTask;
}
