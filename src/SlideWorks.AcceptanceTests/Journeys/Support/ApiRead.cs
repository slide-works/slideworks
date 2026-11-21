namespace SlideWorks.AcceptanceTests.Journeys.Support;

// What a signed-in page's GET of a JSON path returned: the status, the body, and one string field of it (null when the
// body has no such string). The test asserts on these; the helper that fetched them asserts nothing.
public sealed record ApiRead(int Status, string Body, string? Value);
