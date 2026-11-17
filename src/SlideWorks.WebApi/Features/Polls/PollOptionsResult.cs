namespace SlideWorks.WebApi.Features.Polls;

// What add_poll_option returns: the poll's options after the change, in order. An object, because an MCP result's
// structured content must be one.
public sealed record PollOptionsResult(IReadOnlyList<string> Options);
