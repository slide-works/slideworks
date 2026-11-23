namespace SlideWorks.WebApi.Features.Sharing.Limits;

// The share log (D13, D33). Nothing here lists, searches or returns these rows to a person (D32).
public interface IShareRecipients
{
    // Writes one ShareRecipientRow per email, AddedAt from the clock, on the request's AppDbContext. It opens and commits
    // no transaction of its own, so inside the share's write transaction the rows commit or roll back with the share. An
    // empty list writes nothing.
    Task RecordAsync(Guid sharerId, string deckId, IReadOnlyList<string> emails);

    // One entry per distinct email the sharer recorded at or after since, across all their decks, with its latest
    // AddedAt, oldest first.
    Task<IReadOnlyList<RecentRecipient>> DistinctSinceAsync(Guid sharerId, DateTimeOffset since);
}
