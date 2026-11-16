namespace SlideWorks.WebApi.Tests.Support;

// What JsonShape relaxes. Paths are dotted property names from the root, with "[]" for "every element of this array":
// "lastChange", "decks[].subtitle", "slides[].slots". NullablePaths may be null in the actual JSON. At an OpaquePaths
// path the actual value must have the example's kind (or be null, if that path is also nullable), and nothing inside it
// is compared, for values whose shape varies by element, such as each slide's slots, which depend on its layout.
public sealed record JsonShapeOptions
{
    public IReadOnlyList<string> NullablePaths { get; init; } = [];

    public IReadOnlyList<string> OpaquePaths { get; init; } = [];
}
