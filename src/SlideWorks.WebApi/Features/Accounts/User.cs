namespace SlideWorks.WebApi.Features.Accounts;

// A person who has signed in with Google. Stored through IUserDirectory, which keys on the Google subject and keeps the
// email lower-cased.
public class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string Name { get; set; }

    public string? PictureUrl { get; set; }

    public required string GoogleSubject { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
