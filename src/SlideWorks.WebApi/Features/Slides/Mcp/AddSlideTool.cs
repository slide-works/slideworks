using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

[McpServerToolType]
public sealed class AddSlideTool
{
    [McpServerTool(Name = SlideCommands.ADD_TOOL)]
    [Description("Add a slide to a deck you own. Pick a layout (list_layouts gives each one's slots) and fill its slots with plain text: no Markdown or HTML. A slide that can't render is refused with an error naming the field, such as \"practice.do[2].lead: required\". Returns the slide's number, its steps, its revision and its url.")]
    public Task<CallToolResult> AddSlide(
        SlideCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The layout's name, such as title, bullets or practice.")] string layout,
        [Description("The layout's slots as a JSON object.")] JsonObject slots,
        [Description("The 1-based part this slide belongs to, or leave it out for none.")] int? part = null,
        [Description("The 1-based number the new slide takes; later slides move down. Leave it out to add the slide at the end.")] int? position = null,
        [Description("Speaker notes. Viewers never see them.")] string? notes = null)
        => McpToolErrors.Run(() => commands.AddAsync(new AddSlideRequest(deck, layout, JsonSerializer.SerializeToElement(slots), part, position, notes)));
}
