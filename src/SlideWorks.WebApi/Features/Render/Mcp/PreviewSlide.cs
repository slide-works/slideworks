using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Render.Mcp;

// One slide of preview_deck's result (L7): its 1-based number and the URL to screenshot, with the Preview token in the
// fragment so it never reaches the server.
public sealed record PreviewSlide(int Number, [property: JsonPropertyName("render_url")] string RenderUrl);
