using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// A YouTube link. It has to pass the link checks first (so it can never be a script or a user-info
// address), and then it has to be one video on YouTube. The text is stored unchanged.
public sealed class YouTubeSlot : ISlotType
{
    public const string NOT_A_VIDEO = "must be a link to one YouTube video, such as https://www.youtube.com/watch?v=M7lc1UVf-VE";

    public string Expected => "a YouTube link";

    public string Kind => "youtube";

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "string",
        ["format"] = "uri",
        ["maxLength"] = LinkSlot.MAX_LENGTH,
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var text = value.GetString() ?? "";
        var problem = LinkSlot.Problem(text);
        if (problem is not null)
        {
            validation.Fail(path, problem);
            return;
        }

        if (!YouTubeVideo.TryRead(text, out _, out _))
        {
            validation.Fail(path, NOT_A_VIDEO);
        }
    }
}
