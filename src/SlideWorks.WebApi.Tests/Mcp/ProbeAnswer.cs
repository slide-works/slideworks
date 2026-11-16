namespace SlideWorks.WebApi.Tests.Mcp;

// probe_throw's result when nothing throws: an object, so it goes back as structured content.
public sealed record ProbeAnswer(int Answer, IReadOnlyList<string> Items);
