using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look;

// The change log's one entry for a look change (L8): joined clauses, lowercase and read after the actor like SW-1's
// entries ("You · changed the template to workshop and the palette to Ember"). The template is named, the palette by its
// label. A palette that a locking template forced is named too, though the caller didn't ask for it.
public static class LookSummary
{
    public static string Of(LookChange change, Template template, Palette palette, bool paletteChanged)
    {
        var changed = new List<string>();
        if (change.Template is not null)
        {
            changed.Add($"the template to {template.Name}");
        }
        if (change.Palette is not null || paletteChanged)
        {
            changed.Add($"the palette to {palette.Label}");
        }
        if (change.Draft is { } draft)
        {
            changed.Add($"the draft mark to {OnOff(draft)}");
        }
        if (change.Chrome is { } chrome)
        {
            AddChrome(changed, "the tracker", chrome.Tracker, OnOff);
            AddChrome(changed, "the footer", chrome.Footer, footer => footer!);
            AddChrome(changed, "the progress bar", chrome.Progress, OnOff);
            if (chrome.JoinBar is { } joinBar)
            {
                changed.Add($"the join bar to {OnOff(joinBar)}");
            }
        }

        var clauses = new List<string>();
        if (changed.Count > 0)
        {
            clauses.Add($"changed {Join(changed)}");
        }
        if (change.Logo is not null)
        {
            clauses.Add(change.Logo.ImageId is null ? "removed the logo" : "set the logo");
        }
        return Join(clauses);
    }

    private static void AddChrome<T>(List<string> changed, string what, Change<T>? key, Func<T, string> label)
    {
        if (key is null)
        {
            return;
        }
        changed.Add(key.Value is null ? $"{what} to the template's default" : $"{what} to {label(key.Value)}");
    }

    private static string OnOff(bool? value) => value == true ? "on" : "off";

    private static string Join(IReadOnlyList<string> items)
        => items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";
}
