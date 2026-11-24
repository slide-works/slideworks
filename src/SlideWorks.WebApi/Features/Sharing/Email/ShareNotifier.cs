using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// Hands the notice to the worker's in-process queue and returns: the share has committed, and nothing about the email can
// fail it (D15).
[RegisterPerRequest]
public sealed class ShareNotifier(ShareEmailWorker worker) : IShareNotifier
{
    public Task QueueAsync(ShareNotice notice)
    {
        worker.Enqueue(notice);
        return Task.CompletedTask;
    }
}
