using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

[McpServerToolType]
public sealed class AddSlideTool
{
    [McpServerTool(Name = SlideCommands.ADD_TOOL)]
    [Description("Add a slide to a deck you own. Pick a layout (list_layouts gives each one's slots) and fill its slots with plain text: no Markdown or HTML. A slide that can't render is refused with an error naming the field, such as \"practice.do[2].lead: required\". Returns the slide's number, its steps, its revision, its url and warnings: a layout outside the deck's template is kept and still renders, with a warning, so consider another layout.")]
    public Task<CallToolResult> AddSlide(
        SlideCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The layout's name, as list_layouts names it. list_layouts with the deck's template gives the layouts it allows.")] string layout,
        [Description("The layout's slots as a JSON object. Required.")] JsonElement? slots = null,
        [Description("The 1-based part this slide belongs to, or leave it out for none.")] int? part = null,
        [Description("The 1-based number the new slide takes; later slides move down. Leave it out to add the slide at the end.")] int? position = null,
        [Description("Speaker notes. Viewers never see them.")] string? notes = null,
        [Description("true marks the slide as a deep dive: it shows the deep-dive mark, a divider that lists its part's deep dives lists it, and its notes read with a prefix telling the speaker to skip it if short of time. Leave it out for an ordinary slide.")] bool? deep_dive = null)
        => McpToolErrors.Run(() => commands.AddAsync(new AddSlideRequest(deck, layout, slots, part, position, notes, deep_dive)));
}
