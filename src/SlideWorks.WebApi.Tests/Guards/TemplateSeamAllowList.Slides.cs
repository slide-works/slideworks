namespace SlideWorks.WebApi.Tests.Guards;

// The files that may still name Core's template catalogue outside Features/Templates, by folder. Each is a caller from
// before ITemplates; a list only shrinks, and is empty once its folder asks ITemplates.
public static partial class TemplateSeamAllowList
{
    public static IReadOnlyList<string> Slides { get; } = [];
}
