using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Checks.Mcp;

[McpServerToolType]
public sealed class CheckDeckTool
{
    public const string NAME = "check_deck";

    [McpServerTool(Name = NAME, ReadOnly = true)]
    [Description("Check a deck you own for layout problems: text that runs off the slide or under the footer, text that no longer fits after a look change, small text, low contrast, text on images, images that are missing, blurry or have generic alt text. Renders each slide (or the slides you list), so it takes a few seconds per slide. Returns findings, each with the slide, rule, severity, where and a message saying what to change. Fix every error and warning, then check again. With translations on it also renders each translated slide in every language, so give language or slides to check less. Each finding names its language.")]
    public Task<CallToolResult> CheckDeck(
        DeckChecks checks,
        [Description("The deck's id.")] string deck,
        [Description("The 1-based slide numbers to check. Leave out for every slide.")] int[]? slides = null,
        [Description("A language code such as \"th\" to check only that language. Leave out for the primary and every translation.")] string? language = null)
        => McpToolErrors.Run(() => checks.CheckAsync(deck, slides, true, language));
}
