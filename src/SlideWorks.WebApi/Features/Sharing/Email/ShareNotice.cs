namespace SlideWorks.WebApi.Features.Sharing.Email;

// The people newly added to a deck by one share: who shared which deck, and whom to tell (normalised emails). No access
// level: the email never says it (C8).
public sealed record ShareNotice(string DeckId, Guid SharerId, IReadOnlyList<string> Added);
