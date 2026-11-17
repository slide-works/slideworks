using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts;

// A custom layout that passed every check: what is stored for an upload and what a slide is validated
// and drawn with. Css and Tree both have every class written with the scoped prefix
// (LayoutClassPrefix), and Tree carries the scope: the client uses the class names as given.
// SlotsJson is the uploaded slots.json; SampleJson and MaximumJson are slots for the review page.
public sealed record CompiledLayout(
    int CompilerVersion,
    string Name,
    string Description,
    string Surface,
    CustomSteps Steps,
    LayoutDefinition Definition,
    LayoutTree Tree,
    string Css,
    string SlotsJson,
    string SampleJson,
    string MaximumJson)
{
    // Stamped on every layout compiled now. A later, stricter sanitiser raises it, finds older rows
    // by it and compiles them again from the kept upload. Nothing filters on it yet.
    public const int COMPILER_VERSION = 1;
}
