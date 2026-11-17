using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SlideWorks.WebApi.Features.Live;

namespace SlideWorks.WebApi.Tests.Live;

// Holds the first save that inserts an AudienceSession until Before has finished, once. A test uses it to let a second
// Go live commit between the first call's check for an open session and its insert, so the race is not left to chance.
public sealed class OneShotSaveGate : SaveChangesInterceptor
{
    private int _used;

    public Func<Task>? Before { get; set; }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var inserts = eventData.Context?.ChangeTracker.Entries<AudienceSession>().Any(e => e.State == EntityState.Added) == true;
        if (inserts && Before != null && Interlocked.Exchange(ref _used, 1) == 0)
        {
            await Before();
        }
        return result;
    }
}
