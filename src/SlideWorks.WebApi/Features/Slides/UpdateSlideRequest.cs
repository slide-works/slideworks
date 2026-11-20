using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Slides;

// update_slide's arguments (K6, L7). Slide is the 1-based number and Revision the one get_deck gave for it. A null Layout,
// Part or DeepDive keeps the slide's own. A missing or null slots never gets here: McpArgumentCheck refuses it on the wire
// as "slots: required". A null Surface keeps the slide's own; a Change holding null clears it, back to the layout's own.
public sealed record UpdateSlideRequest(string Deck, int Slide, long Revision, JsonElement Slots, string? Layout, int? Part, bool? DeepDive = null, Change<string?>? Surface = null, Change<string?>? Notes = null)
{
    // The surface as the tool got it: left out keeps the slide's own, a JSON null clears it, and a word sets it
    // (SlideChecks.SurfaceWord: a value that is not text is refused by the validator as a wrong word).
    public static Change<string?>? SurfaceOf(JsonElement? given) => given?.ValueKind switch
    {
        null or JsonValueKind.Undefined => null,
        JsonValueKind.Null => new Change<string?>(null),
        _ => new Change<string?>(SlideChecks.SurfaceWord(given)),
    };

    // Left out keeps the notes. A JSON null, or blank text, clears them. A string sets them.
    public static Change<string?>? NotesOf(JsonElement? given) => given?.ValueKind switch
    {
        null or JsonValueKind.Undefined => null,
        JsonValueKind.Null => new Change<string?>(null),
        JsonValueKind.String => NotesText(given.Value.GetString()),
        _ => throw new BadRequestException("notes: plain text, or null to clear"),
    };

    private static Change<string?> NotesText(string? text)
    {
        if (text is { Length: > SlideChecks.NOTES_MAX_LENGTH })
        {
            throw new BadRequestException($"notes: at most {SlideChecks.NOTES_MAX_LENGTH} characters");
        }

        return new Change<string?>(string.IsNullOrWhiteSpace(text) ? null : text);
    }
}
