namespace SlideWorks.WebApi.Features.Sharing.Email;

// One person newly shared with: who shared which deck, and whom to tell. No access level: the email never says it (C8).
public sealed record ShareNotice(Guid SharerId, string DeckId, string RecipientEmail);
