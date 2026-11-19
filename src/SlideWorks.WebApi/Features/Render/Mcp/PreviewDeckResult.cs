using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Render.Mcp;

// preview_deck (L7, D13, D21): one render URL template (the literal {slide} where the number goes) and the slide numbers, plus Markdown instructions. The pictures themselves are MCP image
// content, not this object: images_included is only how many of them came back.
public sealed record PreviewDeckResult(
    [property: JsonPropertyName("render_url")] string RenderUrl,
    IReadOnlyList<int> Slides,
    PreviewViewport Viewport,
    [property: JsonPropertyName("wait_for")] string WaitFor,
    [property: JsonPropertyName("expires_in_seconds")] int ExpiresInSeconds,
    [property: JsonPropertyName("player_url")] string PlayerUrl,
    [property: JsonPropertyName("share_card_url")] string ShareCardUrl,
    string Instructions,
    [property: JsonPropertyName("images_included")] int ImagesIncluded);
