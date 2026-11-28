using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Translations.Mcp;

[McpServerToolType]
public sealed class SetTranslationsTool
{
    [McpServerTool(Name = "set_translations", Idempotent = true)]
    [Description("Turn a deck's slide translations on or off, set its primary language and choose the languages it is offered in. You must be able to edit the deck. Give only what to change; the rest keeps its value. Returns a sentence and the settings, with each language's code, name and how many lines are current, stale or missing.")]
    public Task<CallToolResult> SetTranslations(
        TranslationsAgentAccess access,
        [Description("The deck's id.")] string deck,
        [Description("True turns translations on, false turns them off. Stored translations are kept.")] bool? enabled = null,
        [Description("The language the slides are written in, such as \"en\".")] string? primary = null,
        [Description("The language codes the deck is offered in, such as [\"es\",\"th\"].")] IReadOnlyList<string>? languages = null)
        => McpToolErrors.Run(() => access.SetAsync(deck, new SetTranslationsChange(enabled, primary, languages)));
}
