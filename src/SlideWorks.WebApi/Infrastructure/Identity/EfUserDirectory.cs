using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

[RegisterPerRequest(For = typeof(IUserDirectory))]
public class EfUserDirectory(AppDbContext db, TimeProvider clock) : IUserDirectory
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
        return user;
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
