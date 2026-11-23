namespace SlideWorks.WebApi.Features.Sharing.Email;

// An address that has unsubscribed from share emails.
public class EmailSuppression
{
    // Normalised by ViewerEmails.Normalize.
    public required string Email { get; set; }

    public DateTimeOffset At { get; set; }
}
