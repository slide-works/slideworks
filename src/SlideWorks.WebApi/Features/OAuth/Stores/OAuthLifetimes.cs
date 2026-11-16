namespace SlideWorks.WebApi.Features.OAuth.Stores;

// How long each OAuth artefact lives (D20). The in-memory and real stores share these.
public static class OAuthLifetimes
{
    public static readonly TimeSpan Request = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan Code = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan AccessToken = TimeSpan.FromHours(1);

    public static readonly TimeSpan RefreshToken = TimeSpan.FromDays(30);
}
