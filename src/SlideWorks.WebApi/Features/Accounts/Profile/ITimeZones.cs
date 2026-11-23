namespace SlideWorks.WebApi.Features.Accounts.Profile;

public interface ITimeZones
{
    // Every time zone this server knows, as an IANA id and a label, in label order.
    IReadOnlyList<TimeZoneOption> List();

    // Sets the person's time zone to an id from List(), or clears it with null.
    Task SetAsync(Guid userId, string? id);
}

public sealed record TimeZoneOption(string Id, string Label);
