namespace SlideWorks.WebApi.Features.Sharing.Email;

// One row per share email sent, counted for the per-recipient daily limit.
public class ShareEmailRow
{
    public Guid Id { get; set; }

    public required string RecipientEmail { get; set; }

    public Guid SharerId { get; set; }

    public required string DeckId { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
