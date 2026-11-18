using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Templates.Views;

// PUT /api/templates/{id}/sharing: the level and, for named, the viewers' e-mails.
public sealed record TemplateSharingUpdate([property: JsonRequired] DeckSharing Level, IReadOnlyList<string>? Viewers);
