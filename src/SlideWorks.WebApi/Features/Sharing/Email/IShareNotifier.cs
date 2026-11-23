namespace SlideWorks.WebApi.Features.Sharing.Email;

public interface IShareNotifier
{
    // Queues the share email for after the share commits. Never throws for a failed send and never fails the share.
    Task QueueAsync(ShareNotice notice);
}
