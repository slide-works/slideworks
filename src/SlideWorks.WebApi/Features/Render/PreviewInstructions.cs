using System.Reflection;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Render;

// Fills in preview_deck's instructions (L7, R2) from the embedded PreviewInstructions.md: how an agent with a shell
// checks for and installs headless Chromium, then screenshots each slide's render URL and looks at the PNGs (D21).
[RegisterSingleton]
public class PreviewInstructions
{
    // The Playwright the agent installs, pinned to the version the repo uses (src/SlideWorks.Client/package.json and
    // the WebApi's Microsoft.Playwright), so the commands behave the same on every machine.
    public const string PLAYWRIGHT_VERSION = "1.62.0";
    public const int WIDTH = 1920;
    public const int HEIGHT = 1080;
    // The page settles either way (L7, amended): data-ready when the slide is drawn, data-error with the text drawn on the
    // page. The screenshot never waits on a page that will never be ready.
    public const string WAIT_FOR = "[data-ready=true], [data-error]";
    // The CLI waits forever by default; a minute covers a cold browser and the slide's fonts and images.
    public const int TIMEOUT_MS = 60000;

    private readonly string _template = Read();

    // renderUrl holds the literal {slide}; the loop puts $n there. 1..N is written as $(seq 1 N), anything else as a list.
    public string Fill(string renderUrl, IReadOnlyList<int> slides, string playerUrl, TimeSpan expiresIn)
    {
        var consecutive = slides.Count > 0 && slides.Select((n, i) => n == i + 1).All(x => x);
        var numbers = consecutive ? $"$(seq 1 {slides.Count})" : string.Join(' ', slides);
        var loop = slides.Count == 0
            ? "# The deck has no slides yet."
            : $"for n in {numbers}; do npx -y playwright@{PLAYWRIGHT_VERSION} screenshot --viewport-size={WIDTH},{HEIGHT} --wait-for-selector=\"{WAIT_FOR}\" --timeout={TIMEOUT_MS} \"{renderUrl.Replace("{slide}", "$n")}\" slide-$n.png; done";
        return _template
            .Replace("{{version}}", PLAYWRIGHT_VERSION)
            .Replace("{{minor}}", PLAYWRIGHT_VERSION[..PLAYWRIGHT_VERSION.LastIndexOf('.')])
            .Replace("{{timeout_seconds}}", (TIMEOUT_MS / 1000).ToString())
            .Replace("{{width}}", WIDTH.ToString())
            .Replace("{{height}}", HEIGHT.ToString())
            .Replace("{{loop}}", loop)
            .Replace("{{url}}", renderUrl)
            .Replace("{{wait}}", WAIT_FOR)
            .Replace("{{timeout_ms}}", TIMEOUT_MS.ToString())
            .Replace("{{list}}", string.Join(", ", slides))
            .Replace("{{minutes}}", ((int)expiresIn.TotalMinutes).ToString())
            .Replace("{{player_url}}", playerUrl);
    }

    private static string Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SlideWorks.WebApi.Features.Render.PreviewInstructions.md")
            ?? throw new InvalidOperationException("PreviewInstructions.md is not embedded in the WebApi assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
