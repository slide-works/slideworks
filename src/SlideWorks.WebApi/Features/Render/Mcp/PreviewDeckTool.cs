using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Render.Mcp;

[McpServerToolType]
public sealed class PreviewDeckTool
{
    [McpServerTool(Name = "preview_deck", ReadOnly = true)]
    [Description("Preview a deck you own by screenshotting its slides yourself. Returns one render URL template with {slide} in it, the slide numbers to put there (every slide unless you list some) and instructions: install headless Chromium if it's missing, screenshot each URL at 1920x1080, then look at every PNG before you say the deck is done. The URL expires in 10 minutes.")]
    public Task<CallToolResult> PreviewDeck(
        DeckPreviews previews,
        [Description("The deck's id.")] string deck,
        [Description("The 1-based slide numbers to preview. Leave out for every slide.")] int[]? slides = null)
        => McpToolErrors.Run(() => previews.PreviewAsync(deck, slides));
}
