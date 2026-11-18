using System.Text.Json;

namespace SlideWorks.WebApi.Features.Look.Views;

// One custom layout of the deck's template in a LookView (T7): its checked node tree, its css and its slots, as the
// client draws them. No template sends one yet.
public sealed record LookCustomLayoutView(string Name, string Surface, JsonElement Tree, string Css, JsonElement Slots);
