using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// Where people are stored. A person is keyed by their Google subject, and emails are kept lower-cased.
public interface IUserDirectory
{
    // The first admin's email. While no admin exists, a sign-in with this email (any case, spaces trimmed) is made an
    // approved admin. Once any admin exists it does nothing. Read on every upsert, so it can be set without a restart.
    const string ADMIN_EMAIL_SETTING = "SLIDEWORKS_ADMIN_EMAIL";

    // Creates the person on first sign-in as Pending, and updates their email, name and picture on later ones. It never
    // changes Access or IsAdmin, except for the ADMIN_EMAIL_SETTING bootstrap. Throws EmailLinkedException when the email
    // belongs to a person with another Google subject.
    Task<User> UpsertAsync(string googleSubject, string email, string name, string? pictureUrl);

    // Case-insensitive.
    Task<User?> FindByEmailAsync(string email);
}
