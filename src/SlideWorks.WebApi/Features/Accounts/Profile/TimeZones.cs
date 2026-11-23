using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

// List is real. TODO: SetAsync should check the id against List() and save it on the user.
[RegisterPerRequest]
public sealed class TimeZones : ITimeZones
{
    public IReadOnlyList<TimeZoneOption> List()
        => TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => new TimeZoneOption(IanaId(zone), zone.DisplayName))
            .DistinctBy(option => option.Id)
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToList();

    public Task SetAsync(Guid userId, string? id) => throw new NotImplementedException("Time zone editing is not built yet.");

    private static string IanaId(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
        {
            return zone.Id;
        }
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
    }
}
