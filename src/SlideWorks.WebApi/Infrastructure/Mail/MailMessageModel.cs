namespace SlideWorks.WebApi.Infrastructure.Mail;

// One email. UnsubscribeUrl, when set, also goes in a List-Unsubscribe header.
public sealed record MailMessageModel(string To, string Subject, string Text, string Html, string? UnsubscribeUrl);
