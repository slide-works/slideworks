namespace SlideWorks.WebApi.Features.Accounts;

// A person who has signed in with Google. Stored through IUserDirectory, which keys on the Google subject and keeps the
// email lower-cased. Access and admin change through IUserAccounts (or the SLIDEWORKS_ADMIN_EMAIL bootstrap in
// IUserDirectory), never by a sign-in.
public class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string Name { get; set; }

    public string? PictureUrl { get; set; }

    public required string GoogleSubject { get; set; }

    // When they first signed in, which is also when they asked for access.
    public DateTimeOffset CreatedAt { get; set; }

    public UserAccess Access { get; set; } = UserAccess.Pending;

    public bool IsAdmin { get; set; }

    // When an admin, the bootstrap or a test seam last changed Access or IsAdmin. Null until then.
    public DateTimeOffset? AccessChangedAt { get; set; }

    // The admin who made that change, or null for the system (the SLIDEWORKS_ADMIN_EMAIL bootstrap or a test seam).
    public Guid? AccessChangedBy { get; set; }
}
