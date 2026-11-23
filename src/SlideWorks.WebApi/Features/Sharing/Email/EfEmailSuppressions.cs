using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Sharing.Email;

[RegisterPerRequest(For = typeof(IEmailSuppressions))]
public class EfEmailSuppressions(AppDbContext db, TimeProvider clock) : IEmailSuppressions
{
    private const int SQLITE_CONSTRAINT = 19;

    public async Task AddAsync(string email)
    {
        var normalised = ViewerEmails.Normalize(email);
        if (await db.EmailSuppressions.AnyAsync(s => s.Email == normalised))
        {
            return;
        }
        db.EmailSuppressions.Add(new EmailSuppression { Email = normalised, At = clock.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteErrorCode: SQLITE_CONSTRAINT })
        {
            // A second unsubscribe for the same address won the insert: the address is suppressed either way.
            db.ChangeTracker.Clear();
        }
    }

    public Task<bool> ContainsAsync(string email)
    {
        var normalised = ViewerEmails.Normalize(email);
        return db.EmailSuppressions.AnyAsync(s => s.Email == normalised);
    }
}
