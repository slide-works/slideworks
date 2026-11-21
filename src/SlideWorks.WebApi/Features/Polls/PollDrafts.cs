using System.Text.Json;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// The poll object on add_slide and add_slides: the settings a poll slide is born with, so it is complete in that one call.
public static class PollDrafts
{
    public static PollSettings Settings(JsonElement poll, int optionCount)
    {
        if (poll.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException("poll: an object of pick, max, correct, results, opens and answers");
        }

        string? pick = null;
        string? results = null;
        string? opens = null;
        string? answers = null;
        int? max = null;
        int? correct = null;
        foreach (var property in poll.EnumerateObject())
        {
            switch (property.Name)
            {
                case "pick":
                    pick = Text(property);
                    break;
                case "results":
                    results = Text(property);
                    break;
                case "opens":
                    opens = Text(property);
                    break;
                case "answers":
                    answers = Text(property);
                    break;
                case "max":
                    max = Whole(property);
                    break;
                case "correct":
                    correct = Whole(property);
                    break;
                default:
                    throw new BadRequestException($"poll.{property.Name}: unknown field");
            }
        }

        return PollSettingsCommands.Merge(PollSettings.Default, new PollSettingsChange(pick, max, results, correct, opens, answers), optionCount);
    }

    // How many options the slots carry. An open question sends an empty list.
    public static int OptionCount(JsonElement slots)
        => slots.ValueKind == JsonValueKind.Object && slots.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array
            ? options.GetArrayLength()
            : 0;

    private static string Text(JsonProperty property)
        => property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()!
            : throw new BadRequestException($"poll.{property.Name}: must be text");

    // TryGetInt32 throws on anything but a number, so a quoted "2" is checked for its kind first and refused like any other
    // value that is not a whole number.
    private static int Whole(JsonProperty property)
        => property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number)
            ? number
            : throw new BadRequestException($"poll.{property.Name}: must be a whole number");
}
