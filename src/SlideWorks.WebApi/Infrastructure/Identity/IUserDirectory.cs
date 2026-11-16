using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// Where people are stored. A person is keyed by their Google subject, and emails are kept lower-cased.
public interface IUserDirectory
{
    // Creates the person on first sign-in, and updates their email, name and picture on later ones.
    Task<User> UpsertAsync(string googleSubject, string email, string name, string? pictureUrl);

    // Case-insensitive.
    Task<User?> FindByEmailAsync(string email);
}
