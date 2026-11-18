using System.Globalization;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The page that measures an uploaded font. The font is attacker-supplied bytes, so the page holds them only as a
// base64 data: URL inside a CSS string, and base64 has no character that can end the string, the url() or the style
// element. A meta CSP then allows nothing but data: fonts: no script, no request, no frame. The only script is the
// constant MEASURE, which the engine evaluates; the upload is never part of it.
public static class FontMeasurePage
{
    public const string FAMILY = "SwMeasure";
    public const int SIZE_PX = 16;

    // 210 characters, set at 16 px with no wrapping.
    public const string SAMPLE = "Ship Smaller Changes More Often: Measure What Customers Do, Not What Teams Say. Plan 2026 Budgets Around 12 Weekly Releases, Review Open Risks Every Monday, And Keep One Owner For Each Decision The Group Makes.";

    public const string SAMPLE_ID = "sample";

    // Loads the face (rejecting when the browser cannot decode it) and answers the line's width in pixels.
    public const string MEASURE = "async () => { try { await document.fonts.load('16px SwMeasure', document.getElementById('sample').textContent); } "
        + "catch (e) { throw new Error('font not loaded'); } "
        + "if (!document.fonts.check('16px SwMeasure')) { throw new Error('font not loaded'); } "
        + "return document.getElementById('sample').getBoundingClientRect().width; }";

    public static string Html(byte[] woff2)
    {
        var data = Convert.ToBase64String(woff2);
        return "<!doctype html><html><head><meta charset=\"utf-8\">"
            + "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; font-src data:; style-src 'unsafe-inline'\">"
            + $"<style>@font-face {{ font-family: \"{FAMILY}\"; src: url(\"data:font/woff2;base64,{data}\") format(\"woff2\"); }}"
            + $"body {{ margin: 0; }} #{SAMPLE_ID} {{ font-family: \"{FAMILY}\"; font-size: {SIZE_PX}px; white-space: nowrap; display: inline-block; }}</style>"
            + $"</head><body><span id=\"{SAMPLE_ID}\">{SAMPLE}</span></body></html>";
    }

    // The width of the line in pixels / (its characters x 16): the raw width on the font's scale.
    public static double Raw(double widthPx) => widthPx / (SAMPLE.Length * (double)SIZE_PX);

    public static string Describe(double raw) => raw.ToString("0.000", CultureInfo.InvariantCulture);
}
