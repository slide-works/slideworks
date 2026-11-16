using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

[RegisterPerRequest(For = typeof(IUserDirectory))]
public class EfUserDirectory(AppDbContext db, TimeProvider clock) : IUserDirectory
{
    public async Task<User> UpsertAsync(string googleSubject, string email, string name, string? pictureUrl)
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

    public Task<User?> FindByEmailAsync(string email)
    {
        var normalised = Normalise(email);
        return db.Users.SingleOrDefaultAsync(u => u.Email == normalised);
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
