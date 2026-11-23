namespace SlideWorks.WebApi.Infrastructure.Mail;

public interface IMailSender
{
    // Sends one email. Throws when the send fails; callers decide what a failure means.
    Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default);
}
