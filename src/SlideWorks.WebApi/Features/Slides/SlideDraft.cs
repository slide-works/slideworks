using System.Text.Json;

namespace SlideWorks.WebApi.Features.Slides;

// One slide in an add_slides batch. Position is the batch's, not the item's.
public sealed record SlideDraft(
    string Layout,
    JsonElement Slots,
    int? Part,
    string? Notes,
    bool? DeepDive,
    string? Surface,
    JsonElement? Poll = null);
