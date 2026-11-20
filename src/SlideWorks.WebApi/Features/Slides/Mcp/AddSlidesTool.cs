using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

// A batch of slides in the order given. One that does not fit is reported and left out; the others are stored.
[McpServerToolType]
public sealed class AddSlidesTool
{
    [McpServerTool(Name = SlideCommands.ADD_MANY_TOOL)]
    [Description("Add several slides to a deck you own, in the order given. Each entry is a layout, its slots, and optionally part, notes, deep_dive, surface and poll, as add_slide takes them. A poll entry can carry its settings, including answers text or cloud, so the poll is complete when this returns. A slide that cannot render is reported and left out: it does not move the others and it does not take a number. The slides that fit are stored together at one position. Returns each slide's fit, and for each stored slide its number, steps, revision, url and warnings.")]
    public Task<CallToolResult> AddSlides(
        SlideCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The slides, in the order to add them.")] JsonElement slides,
        [Description("Where the first stored slide goes: a slide number, or \"end\". Leave it out for the end. A number past the end is refused and nothing is stored.")] JsonElement? position = null)
        => McpToolErrors.Run(() => commands.AddManyAsync(deck, Read(slides), PositionOf(position)));

    private static int? PositionOf(JsonElement? position)
    {
        if (position is null || position.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (position.Value.ValueKind == JsonValueKind.String && position.Value.GetString() == "end")
        {
            return null;
        }

        if (position.Value.ValueKind == JsonValueKind.Number && position.Value.TryGetInt32(out var number))
        {
            return number;
        }

        throw new BadRequestException("position: a slide number, or \"end\"");
    }

    private static List<SlideDraft?> Read(JsonElement slides)
    {
        if (slides.ValueKind != JsonValueKind.Array)
        {
            throw new BadRequestException("slides: a list");
        }

        var list = new List<SlideDraft?>();
        foreach (var item in slides.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                list.Add(null);
                continue;
            }

            var layout = item.TryGetProperty("layout", out var layoutValue) && layoutValue.ValueKind == JsonValueKind.String
                ? layoutValue.GetString()!
                : "";
            var slots = item.TryGetProperty("slots", out var slotsValue) ? slotsValue : default;
            int? part = item.TryGetProperty("part", out var partValue) && partValue.TryGetInt32(out var partNumber) ? partNumber : null;
            string? notes = item.TryGetProperty("notes", out var notesValue) && notesValue.ValueKind == JsonValueKind.String
                ? notesValue.GetString()
                : null;
            bool? deepDive = item.TryGetProperty("deep_dive", out var deepValue) && deepValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? deepValue.GetBoolean()
                : null;
            string? surface = item.TryGetProperty("surface", out var surfaceValue) && surfaceValue.ValueKind == JsonValueKind.String
                ? surfaceValue.GetString()
                : null;
            JsonElement? poll = item.TryGetProperty("poll", out var pollValue) && pollValue.ValueKind != JsonValueKind.Null
                ? pollValue
                : null;
            list.Add(new SlideDraft(layout, slots, part, notes, deepDive, surface, poll));
        }

        return list;
    }
}
