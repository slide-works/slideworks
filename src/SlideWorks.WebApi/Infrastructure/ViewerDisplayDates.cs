using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Infrastructure;

// Formats dates in the time zone saved by the person making the request (an agent uses the person it acts for).
// Anonymous callers, people with no zone and a stored id this server no longer knows get UTC.
[RegisterPerRequest]
public sealed class ViewerDisplayDates(ICurrentActor actor, AppDbContext db, TimeProvider clock) : IDisplayDates
{
    private TimeZoneInfo? _zone;

    public string Ago(DateTimeOffset at) => DisplayDate.Ago(at, clock.GetUtcNow(), Zone());

    public string Format(DateTimeOffset at) => DisplayDate.Format(at, Zone());

    // The labels are built synchronously inside view builders, so the zone is read once per request, on first use.
    private TimeZoneInfo Zone() => _zone ??= Lookup();

    private TimeZoneInfo Lookup()
    {
        if (actor.PresenterId is not { } id)
        {
            return TimeZoneInfo.Utc;
        }
        var saved = db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.TimeZone).SingleOrDefault();
        return saved is not null && TimeZoneInfo.TryFindSystemTimeZoneById(saved, out var zone) ? zone : TimeZoneInfo.Utc;
    }
}
