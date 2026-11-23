using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

[RegisterPerRequest]
public sealed class TimeZones(AppDbContext db) : ITimeZones
{
    public IReadOnlyList<TimeZoneOption> List()
        => TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => new TimeZoneOption(IanaId(zone), zone.DisplayName))
            .DistinctBy(option => option.Id)
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToList();

    public async Task SetAsync(Guid userId, string? id)
    {
        if (id is not null && !List().Any(option => option.Id == id))
        {
            throw new BadRequestException("That time zone is not one of the choices.");
        }
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId) ?? throw new NotFoundException("The signed-in person no longer exists.");
        user.TimeZone = id;
        await db.SaveChangesAsync();
    }

    private static string IanaId(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
        {
            return zone.Id;
        }
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
    }
}
