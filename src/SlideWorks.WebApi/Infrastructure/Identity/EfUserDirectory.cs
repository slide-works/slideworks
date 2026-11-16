using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// The SLIDEWORKS_ADMIN_EMAIL bootstrap runs inside the upsert: after the person is saved, one conditional update makes them
// an approved admin if their email matches and no admin exists yet.
[RegisterPerRequest(For = typeof(IUserDirectory))]
public class EfUserDirectory(AppDbContext db, TimeProvider clock, IConfiguration configuration) : IUserDirectory
{
    private const int SQLITE_CONSTRAINT = 19;

    // Two first sign-ins for one subject can race: both find nothing and both insert. The loser's insert breaks the
    // unique index, so it starts again, finds the winner's row and updates it.
    public async Task<User> UpsertAsync(string googleSubject, string email, string name, string? pictureUrl)
    {
        try
        {
            return await UpsertOnceAsync(googleSubject, email, name, pictureUrl);
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteErrorCode: SQLITE_CONSTRAINT })
        {
            db.ChangeTracker.Clear();
            return await UpsertOnceAsync(googleSubject, email, name, pictureUrl);
        }
    }

    public Task<User?> FindByEmailAsync(string email)
    {
        var normalised = Normalise(email);
        return db.Users.SingleOrDefaultAsync(u => u.Email == normalised);
    }

    private async Task<User> UpsertOnceAsync(string googleSubject, string email, string name, string? pictureUrl)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == googleSubject);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                GoogleSubject = googleSubject,
                Email = Normalise(email),
                Name = name,
                CreatedAt = clock.GetUtcNow(),
            };
            db.Users.Add(user);
        }
        user.Email = Normalise(email);
        user.Name = name;
        user.PictureUrl = pictureUrl;
        await db.SaveChangesAsync();
        if (IsAdminEmail(user.Email) && await BootstrapAdminAsync(user.Id))
        {
            await db.Entry(user).ReloadAsync();
        }
        return user;
    }

    private bool IsAdminEmail(string normalisedEmail)
    {
        var adminEmail = configuration[IUserDirectory.ADMIN_EMAIL_SETTING]?.Trim();
        return !string.IsNullOrEmpty(adminEmail) && string.Equals(Normalise(adminEmail), normalisedEmail, StringComparison.Ordinal);
    }

    // One conditional update, so "no admin exists" holds at the moment the admin is made.
    private async Task<bool> BootstrapAdminAsync(Guid userId)
    {
        var now = clock.GetUtcNow();
        var changed = await db.Users
            .Where(u => u.Id == userId && !db.Users.Any(other => other.IsAdmin))
            .ExecuteUpdateAsync(set => set
                .SetProperty(u => u.Access, UserAccess.Approved)
                .SetProperty(u => u.IsAdmin, true)
                .SetProperty(u => u.AccessChangedAt, now)
                .SetProperty(u => u.AccessChangedBy, (Guid?)null));
        return changed == 1;
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
