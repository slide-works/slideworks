using System.Reflection;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Render.Mcp;

namespace SlideWorks.WebApi.Features.Render;

// Fills in preview_deck's instructions (L7, R2) from the embedded PreviewInstructions.md: how an agent with a shell
// checks for and installs headless Chromium, then screenshots each render URL and looks at the PNGs (D21).
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

    public string Fill(IReadOnlyList<PreviewSlide> slides, string playerUrl, TimeSpan expiresIn)
    {
        var commands = slides.Select(s =>
            $"npx -y playwright@{PLAYWRIGHT_VERSION} screenshot --viewport-size={WIDTH},{HEIGHT} --wait-for-selector=\"{WAIT_FOR}\" --timeout={TIMEOUT_MS} \"{s.RenderUrl}\" slide-{s.Number}.png");
        return _template
            .Replace("{{version}}", PLAYWRIGHT_VERSION)
            .Replace("{{minor}}", PLAYWRIGHT_VERSION[..PLAYWRIGHT_VERSION.LastIndexOf('.')])
            .Replace("{{timeout_seconds}}", (TIMEOUT_MS / 1000).ToString())
            .Replace("{{width}}", WIDTH.ToString())
            .Replace("{{height}}", HEIGHT.ToString())
            .Replace("{{commands}}", slides.Count == 0 ? "# The deck has no slides yet." : string.Join("\n", commands))
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
