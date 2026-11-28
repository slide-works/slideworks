using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Translations.Mcp;

[McpServerToolType]
public sealed class TranslateSlidesTool
{
    [McpServerTool(Name = "translate_slides")]
    [Description("Write one language's translations for one or more slides of a deck you can edit. Each entry has the slide's 1-based number, the revision you read it at and lines: an object from a slot path to its translated text, or null to clear that line. Returns the language, the revision and each slide's lines with how many are current, stale or missing, and any warnings.")]
    public Task<CallToolResult> TranslateSlides(
        TranslationWrites writes,
        [Description("The deck's id.")] string deck,
        [Description("The language code to write, such as \"th\".")] string language,
        [Description("The slides to translate: each with its number, revision and lines.")] IReadOnlyList<SlideLinesEntry?> slides)
        => McpToolErrors.Run(() => writes.TranslateAsync(deck, new TranslateSlidesChange(language, slides)));
}
