using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Sharing.Limits;

// TODO: count the sharer's distinct recent recipients (IShareRecipients.DistinctSinceAsync) against SharingLimits and refuse over the limit.
[RegisterPerRequest]
public sealed class ShareGuard : IShareGuard
{
    public Task AdmitAsync(Guid sharerId, IReadOnlyList<string> newlyAdded) => Task.CompletedTask;
}
