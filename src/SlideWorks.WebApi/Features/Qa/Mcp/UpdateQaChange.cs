namespace SlideWorks.WebApi.Features.Qa.Mcp;

// update_qa's arguments as the tool bound them: each is null when left out, which keeps the current value. The two enums
// come as their wire names, as update_poll's do. With a slide, only Questions ("open" for on, "off" for off) and Reactions
// are taken, and they set that slide's switch.
public sealed record UpdateQaChange(string? Questions, bool? Review, bool? Names, bool? Reactions, string? ShowOn, int? Slide);
