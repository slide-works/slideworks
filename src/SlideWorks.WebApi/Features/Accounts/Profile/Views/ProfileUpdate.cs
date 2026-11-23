using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Accounts.Profile.Views;

// PUT /api/me/profile.
public sealed record ProfileUpdate([property: JsonRequired] string Name, [property: JsonRequired] string Colour);

// PUT /api/me/time-zone.
public sealed record TimeZoneUpdate([property: JsonRequired] string TimeZone, [property: JsonRequired] bool FromBrowser);
