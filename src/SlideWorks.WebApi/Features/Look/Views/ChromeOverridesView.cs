using System.Text.Json;
using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// A deck's chrome overrides (L5, D17), stored as Deck.ChromeJson. A null field means the template's default; a deck
// with no overrides stores null.
public sealed record ChromeOverridesView(bool? Tracker, string? Footer, bool? Progress)
{
    public static readonly ChromeOverridesView None = new(null, null, null);

    public static ChromeOverridesView From(string? chromeJson)
        => chromeJson is null ? None : JsonSerializer.Deserialize<ChromeOverridesView>(chromeJson, JsonSerializerOptions.Web) ?? None;

    // What Deck.ChromeJson stores for these overrides: null when none is set.
    public string? ToJson() => this == None ? null : JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    public TemplateChrome ApplyTo(TemplateChrome chrome)
        => chrome with
        {
            Tracker = Tracker ?? chrome.Tracker,
            Footer = Footer ?? chrome.Footer,
            Progress = Progress ?? chrome.Progress,
        };
}
