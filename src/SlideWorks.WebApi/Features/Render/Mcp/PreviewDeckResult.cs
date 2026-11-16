using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Render.Mcp;

// preview_deck (L7, D13, D21): one render URL per slide plus Markdown instructions for the agent to screenshot them in its
// own headless Chromium. No image blocks: the server renders no slides.
public sealed record PreviewDeckResult(
    IReadOnlyList<PreviewSlide> Slides,
    PreviewViewport Viewport,
    [property: JsonPropertyName("wait_for")] string WaitFor,
    [property: JsonPropertyName("expires_in_seconds")] int ExpiresInSeconds,
    [property: JsonPropertyName("player_url")] string PlayerUrl,
    [property: JsonPropertyName("share_card_url")] string ShareCardUrl,
    string Instructions);
