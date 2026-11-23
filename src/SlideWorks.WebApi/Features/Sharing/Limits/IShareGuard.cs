namespace SlideWorks.WebApi.Features.Sharing.Limits;

// Decides whether a sharer may add these people now, against the new-people limits (SharingLimits).
public interface IShareGuard
{
    // newlyAdded: the normalised emails this share adds to a deck that were not on it before. Throws TooManyRequestsException,
    // naming when the limit resets, when the sharer is over a limit; returns otherwise.
    Task AdmitAsync(Guid sharerId, IReadOnlyList<string> newlyAdded);
}
