namespace SlideWorks.WebApi.Features.Sharing.Limits;

// One distinct email a sharer newly added to any of their decks, with the latest time it was added.
public sealed record RecentRecipient(string Email, DateTimeOffset LastAddedAt);
