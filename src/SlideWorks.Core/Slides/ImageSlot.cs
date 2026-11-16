using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// { "image": "<imageId>", "alt": "…" }. The id comes from upload_image. Whether the image exists
// in the deck is checked by the caller with the ids the validation returns (K11).
public sealed class ImageSlot : ISlotType
{
    private const int IMAGE_ID_MAX_LENGTH = 64;

    private readonly ObjectSlot _shape = new(
    [
        new SlotField("image", new TextSlot(IMAGE_ID_MAX_LENGTH), true, "The image id that upload_image returned"),
        new SlotField("alt", new TextSlot(200), true, "What the image shows, for screen readers"),
    ]);

    public IReadOnlyList<SlotField> Fields => _shape.Fields;

    public string Expected => "an image object {image, alt}";

    public string Kind => "image";

    public JsonObject ToJsonSchema()
    {
        var schema = _shape.ToJsonSchema();
        schema["description"] = "An uploaded image: { image: <id from upload_image>, alt }";
        schema["properties"]!["image"]!["pattern"] = "^[A-Za-z0-9_-]+$";
        return schema;
    }

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var before = validation.Errors.Count;
        _shape.Validate(value, path, validation);
        if (validation.Errors.Count != before)
        {
            return;
        }

        var id = value.GetProperty("image").GetString()!;
        if (!id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            validation.Fail($"{path}.image", "not an image id (use the id upload_image returned)");
            return;
        }

        validation.AddImage(id);
    }
}
