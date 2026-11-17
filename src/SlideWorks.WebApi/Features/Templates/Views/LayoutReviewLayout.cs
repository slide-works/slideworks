using System.Text.Json;

namespace SlideWorks.WebApi.Features.Templates.Views;

// A custom layout as compiled: its node tree, its CSS and its slots.
public sealed record LayoutReviewLayout(string Name, string Surface, JsonElement Tree, string Css, JsonElement Slots);
