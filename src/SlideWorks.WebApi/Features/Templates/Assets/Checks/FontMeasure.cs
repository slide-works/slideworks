using System.Globalization;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The width of an uploaded font on the scale slide text is fitted with: avgCharEm = 0.56 x raw / REFERENCE, where raw
// is the width of FontMeasurePage.SAMPLE in pixels / (its characters x 16), as the engine measures it. REFERENCE is the
// raw width of the built-in body font (Inter 400, the normal.woff2 fixture) on that line, so a built-in measures
// exactly 0.56, the figure SlideFit was tuned to. The engine runs once per uploaded font; nothing caches it.
[RegisterSingleton]
public sealed class FontMeasure(IRenderEngine engine)
{
    public const double BUILT_IN_EM = 0.56;

    // The raw width of Inter 400 on the sample line, measured by FontMeasureEngineTests on the arr host.
    public const double REFERENCE = 0.518;

    // The em width of the font, rounded to two places. Throws RendererUnavailableException, TimeoutException, or
    // InvalidDataException when the browser cannot decode the font.
    public async Task<double> MeasureAsync(byte[] woff2, CancellationToken ct)
    {
        var raw = await engine.MeasureFontAsync(woff2, ct);
        return Math.Round(BUILT_IN_EM * raw / REFERENCE, 2, MidpointRounding.AwayFromZero);
    }

    public static string Format(double em) => em.ToString("0.00", CultureInfo.InvariantCulture);
}
