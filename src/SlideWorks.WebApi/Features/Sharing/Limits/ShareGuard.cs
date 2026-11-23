using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing.Limits;

// Refuses a share that would take the sharer over the new-people limits (D10, D13, D33). Inside the share's write transaction,
// so SQLite's BEGIN IMMEDIATE serialises concurrent shares and the count cannot go stale. For each window (the last hour, the
// last 24 hours) the count is the distinct emails the sharer added in that window together with the ones being added now, so one
// person added to five decks counts once and a person already counted costs nothing more.
[RegisterPerRequest]
public sealed class ShareGuard(
    IShareRecipients recipients,
    SharingLimits limits,
    IDisplayDates dates,
    TimeProvider clock,
    ILogger<ShareGuard> logger) : IShareGuard
{
    private static readonly TimeSpan HOUR = TimeSpan.FromHours(1);
    private static readonly TimeSpan DAY = TimeSpan.FromDays(1);

    // A log entry is still counted at its own LastAddedAt + window (DistinctSinceAsync includes "at or after"), so the earliest
    // retry that fits is one second later.
    private static readonly TimeSpan TICK = TimeSpan.FromSeconds(1);

    public async Task AdmitAsync(Guid sharerId, IReadOnlyList<string> newlyAdded)
    {
        if (newlyAdded.Count == 0)
        {
            return;
        }
        var now = clock.GetUtcNow();
        var recent = await recipients.DistinctSinceAsync(sharerId, now - DAY);
        var added = newlyAdded.ToHashSet(StringComparer.Ordinal);
        var hour = Check(recent.Where(r => r.LastAddedAt >= now - HOUR).ToList(), added, limits.NewPerHour, HOUR, "an hour");
        var day = Check(recent, added, limits.NewPerDay, DAY, "a day");
        var refusal = Binding(hour, day);
        if (refusal == null)
        {
            return;
        }
        logger.LogWarning("Sharer {SharerId} refused: over {Limit} new people {Window}", sharerId, refusal.Limit, refusal.Window);
        throw new TooManyRequestsException(Message(refusal, dates), refusal.ResetAt == null ? null : refusal.ResetAt.Value - now);
    }

    // The refusal that holds the longest: one with no reset time (the batch alone is too big) outranks any, else the later reset.
    private static Refusal? Binding(Refusal? hour, Refusal? day)
    {
        if (hour == null || day == null)
        {
            return hour ?? day;
        }
        if (hour.ResetAt == null || day.ResetAt == null)
        {
            return hour.ResetAt == null ? hour : day;
        }
        return hour.ResetAt >= day.ResetAt ? hour : day;
    }

    // Null when the union fits. Otherwise the earliest time it fits: each older entry expires LastAddedAt + window later, but an
    // email that is also being added stays in the union, so its expiry frees nothing. No time when the batch alone is too big.
    private static Refusal? Check(IReadOnlyList<RecentRecipient> inWindow, HashSet<string> added, int limit, TimeSpan window, string label)
    {
        var count = inWindow.Count(r => !added.Contains(r.Email)) + added.Count;
        if (count <= limit)
        {
            return null;
        }
        if (added.Count > limit)
        {
            return new Refusal(limit, label, null, added.Count);
        }
        foreach (var entry in inWindow.Where(r => !added.Contains(r.Email)).OrderBy(r => r.LastAddedAt))
        {
            count--;
            if (count <= limit)
            {
                return new Refusal(limit, label, entry.LastAddedAt + window + TICK, added.Count);
            }
        }
        return new Refusal(limit, label, null, added.Count);
    }

    private static string Message(Refusal refusal, IDisplayDates dates)
    {
        var people = refusal.Limit == 1 ? "1 new person" : $"{refusal.Limit} new people";
        var head = $"You can share with {people} {refusal.Window}.";
        return refusal.ResetAt == null
            ? $"{head} This share adds {refusal.Adding}."
            : $"{head} Try again at {dates.Format(refusal.ResetAt.Value)}.";
    }

    private sealed record Refusal(int Limit, string Window, DateTimeOffset? ResetAt, int Adding);
}
