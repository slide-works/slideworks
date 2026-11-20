using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// One slide in an add_slides batch. Position is the batch's, not the item's. Poll is that entry's settings object, or
// null when the entry leaves it out. It is not slots.
public sealed record SlideDraft(
    string Layout,
    JsonElement Slots,
    int? Part,
    string? Notes,
    bool? DeepDive,
    string? Surface,
    JsonElement? Poll = null);
