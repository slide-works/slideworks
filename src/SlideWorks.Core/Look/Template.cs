using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.Core.Look;

// A template (L1): structure, type and shape, never a colour. The built-ins live in
// src/contracts/templates.json, embedded in this assembly and read by TemplateCatalogue.
public sealed record Template
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string Purpose { get; init; }
    public required bool IsDefault { get; init; }
    public required IReadOnlyList<string> Layouts { get; init; }
    public required TemplateChrome Chrome { get; init; }
    public required IReadOnlyList<string> Outline { get; init; }
    public required IReadOnlyList<string> HouseStyle { get; init; }
    public required TemplateType Type { get; init; }
    public required TemplateShape Shape { get; init; }
    public string? TitleArt { get; init; }
    public required string Ornament { get; init; }
    public string? Logo { get; init; }

    // The palette the template brings, if any. With LockPalette, a deck on this template always uses it.
    public string? Palette { get; init; }
    public required bool LockPalette { get; init; }

    // The layouts of the live room, which belong to a talk whatever its template: the join slide, the poll and the questions slide. A template
    // lists the layouts it is about, so these are in none of them, and Allows is the one place that lets them through (the
    // layout warning and list_layouts both ask it).
    private static readonly string[] EveryTemplate = [JoinLayout.NAME, "poll", "qa", "word-cloud", "open-text"];

    public bool Allows(string layout) => EveryTemplate.Contains(layout, StringComparer.Ordinal) || Layouts.Contains(layout, StringComparer.Ordinal);
}
