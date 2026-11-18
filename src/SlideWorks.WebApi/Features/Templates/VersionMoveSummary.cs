namespace SlideWorks.WebApi.Features.Templates;

// The activity wording for a deck that moves to another version of its uploaded template (D7, D14), the one place it is
// written: set_look and "Update my decks" both log it. The label is the name a person typed in the manifest, never the tpl_ ref.
public static class VersionMoveSummary
{
    public static string Of(string templateLabel, int from, int to) => $"Look: {templateLabel} version {from} to {to}";
}
