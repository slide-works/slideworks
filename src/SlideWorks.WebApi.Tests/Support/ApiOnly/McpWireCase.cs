namespace SlideWorks.WebApi.Tests.Support.ApiOnly;

// One tool's raw-wire case: builds the arguments JSON from the seeded state. A case that cannot succeed without heavy
// state names the error it asserts instead (ErrorContains) and is listed with its reason in McpWireCases.ExpectedErrorOnly.
public sealed record McpWireCase(Func<McpWireContext, Task<string>> Arguments, string? ErrorContains = null);
