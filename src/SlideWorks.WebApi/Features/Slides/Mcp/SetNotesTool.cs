using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

[McpServerToolType]
public sealed class SetNotesTool
{
    [McpServerTool(Name = SlideEdits.SET_NOTES_TOOL)]
    [Description("Set the speaker notes of one or more slides in a deck you own, in one change. Viewers never see notes. Empty text clears a slide's notes. Each slide listed gets a new revision. Returns the slides' numbers and the new revision.")]
    public Task<CallToolResult> SetNotes(
        SlideEdits edits,
        [Description("The deck's id.")] string deck,
        [Description("The notes to set: each with a slide's 1-based number and its text, at most 4000 characters.")] IReadOnlyList<SlideNote> notes)
        => McpToolErrors.Run(() => edits.SetNotesAsync(deck, notes));
}
