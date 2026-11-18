namespace SlideWorks.WebApi.Tests.Guards;

// Features/Look. Only LookViews is left, for the one lookup the slide writes still make through it.
public static partial class TemplateSeamAllowList
{
    public static IReadOnlyList<string> Look { get; } =
    [
        "Features/Look/LookViews.cs",
    ];
}
