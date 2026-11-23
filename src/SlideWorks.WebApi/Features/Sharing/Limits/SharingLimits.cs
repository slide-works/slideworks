using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Sharing.Limits;

// The sharing limits from SLIDEWORKS_SHARE_NEW_PER_HOUR (10), SLIDEWORKS_SHARE_NEW_PER_DAY (30) and
// SLIDEWORKS_SHARE_EMAILS_PER_RECIPIENT_PER_DAY (3), read when first resolved. A value that is not a whole number above zero
// leaves the default.
[RegisterSingleton]
public sealed class SharingLimits
{
    public const string NEW_PER_HOUR_KEY = "SLIDEWORKS_SHARE_NEW_PER_HOUR";

    public const string NEW_PER_DAY_KEY = "SLIDEWORKS_SHARE_NEW_PER_DAY";

    public const string EMAILS_PER_RECIPIENT_PER_DAY_KEY = "SLIDEWORKS_SHARE_EMAILS_PER_RECIPIENT_PER_DAY";

    public const int DEFAULT_NEW_PER_HOUR = 10;

    public const int DEFAULT_NEW_PER_DAY = 30;

    public const int DEFAULT_EMAILS_PER_RECIPIENT_PER_DAY = 3;

    public SharingLimits(IConfiguration configuration)
    {
        NewPerHour = Read(configuration, NEW_PER_HOUR_KEY, DEFAULT_NEW_PER_HOUR);
        NewPerDay = Read(configuration, NEW_PER_DAY_KEY, DEFAULT_NEW_PER_DAY);
        EmailsPerRecipientPerDay = Read(configuration, EMAILS_PER_RECIPIENT_PER_DAY_KEY, DEFAULT_EMAILS_PER_RECIPIENT_PER_DAY);
    }

    // Distinct emails a sharer may newly add to any of their decks in an hour.
    public int NewPerHour { get; }

    // Distinct emails a sharer may newly add in 24 hours.
    public int NewPerDay { get; }

    // Share emails one address gets in 24 hours, from all sharers together.
    public int EmailsPerRecipientPerDay { get; }

    private static int Read(IConfiguration configuration, string key, int fallback)
        => int.TryParse(configuration[key]?.Trim(), out var value) && value > 0 ? value : fallback;
}
