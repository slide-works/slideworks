namespace SlideWorks.WebApi.Features.Sharing.Limits;

// The share log, used only to count: one row per email newly added to a deck's people. Never shown to anyone and never
// searched by email.
public class ShareRecipientRow
{
    public Guid Id { get; set; }

    public Guid SharerId { get; set; }

    public required string DeckId { get; set; }

    // Normalised by ViewerEmails.Normalize.
    public required string Email { get; set; }

    public DateTimeOffset AddedAt { get; set; }
}
