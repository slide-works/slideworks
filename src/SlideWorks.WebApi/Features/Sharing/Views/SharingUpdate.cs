namespace SlideWorks.WebApi.Features.Sharing.Views;

// The PUT /api/decks/{id}/sharing body. Level is "private", "link" or "named"; Viewers are email addresses.
public sealed record SharingUpdate(string Level, IReadOnlyList<string> Viewers);
