using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Render.Mcp;

[McpServerToolType]
public sealed class PreviewDeckTool
{
    [McpServerTool(Name = "preview_deck", ReadOnly = true)]
    [Description("Preview a deck you own. Returns one render URL template with {slide} in it, the slide numbers to put there (every slide unless you list some), the rendered images, `findings` (the problems found on those slides: fix every error and warning, then preview again), and instructions: install headless Chromium if it's missing, screenshot each URL at 1920x1080, then look at every PNG before you say the deck is done. The images are already attached, so a blocked URL still leaves you the pictures. The URL expires in 10 minutes.")]
    public async Task<CallToolResult> PreviewDeck(
        DeckPreviews previews,
        [Description("The deck's id.")] string deck,
        [Description("The 1-based slide numbers to preview. Leave out for every slide.")] int[]? slides = null)
    {
        try
        {
            McpArgumentCheck.ArgumentsBound();
            var outcome = await previews.PreviewAsync(deck, slides);
            return McpToolErrors.WithImages(outcome.Result, outcome.Images);
        }
        catch (Exception error) when (error is BadRequestException or NotFoundException or ConflictException)
        {
            return McpToolErrors.Refusal(error.Message);
        }
    }
}
