using SlideWorks.WebApi.Features.Accounts.Avatars;

namespace SlideWorks.WebApi.Features.Accounts.Profile.Views;

// GET /api/me/profile and the answer of every profile write (src/contracts/screens/profile.json, C3): three panels.
public sealed record ProfileView(IdentityPanel Identity, AccountPanel Account, IReadOnlyList<AgentRow> Agents);

// Colour is the key the person chose (null while they chose none, so Avatar.Colour is the hash-picked one); Colours is every key.
public sealed record IdentityPanel(string Name, string Email, AvatarView Avatar, string? Colour, IReadOnlyList<string> Colours, bool HasPhoto);

// TimeZone is an IANA id or null; AdminUrl is always the admin people page (ScreenShapes.Profile does not allow null there); only IsAdmin says whether to show it.
public sealed record AccountPanel(string? TimeZone, IReadOnlyList<TimeZoneOption> TimeZones, bool IsAdmin, string? AdminUrl);

// One connected agent: Id is its token family id; ConnectedLabel says "Unknown" for families from before the time was kept.
public sealed record AgentRow(Guid Id, string ClientName, string ConnectedLabel, string? LastUsedLabel);
