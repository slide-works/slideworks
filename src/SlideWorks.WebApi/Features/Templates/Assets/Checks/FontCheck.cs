using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The check of a font (D8): the bytes must start with the woff2 signature (the file name and content type prove
// nothing), and the width measured in headless Chromium must be MIN_EM to MAX_EM, both allowed. A passing font is
// served as font/woff2 and stores {"avgCharEm": 0.56}. When no browser can run, or the measure times out, the upload
// is refused with renderer_unavailable: a font is never accepted unmeasured.
[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class FontCheck(FontMeasure measure) : IAssetCheck
{
    public const double MIN_EM = 0.45;
    public const double MAX_EM = 0.70;

    private static readonly byte[] WOFF2 = "wOF2"u8.ToArray();
    private static readonly byte[] TRUE_TYPE_NUMBER = [0x00, 0x01, 0x00, 0x00];
    private static readonly byte[] TRUE_TYPE_NAME = "true"u8.ToArray();
    private static readonly byte[] OPEN_TYPE = "OTTO"u8.ToArray();
    private static readonly byte[] WOFF1 = "wOFF"u8.ToArray();

    public TemplateAssetKind Kind => TemplateAssetKind.Font;

    public async Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var head = bytes.AsSpan(0, Math.Min(4, bytes.Length));
        if (!head.SequenceEqual(WOFF2))
        {
            return AssetCheckResult.Refused(TemplateErrorCodes.FONT_NOT_WOFF2, ("found", Describe(head)));
        }

        double em;
        try
        {
            em = await measure.MeasureAsync(bytes, ct);
        }
        catch (Exception e) when (e is RendererUnavailableException or TimeoutException)
        {
            return AssetCheckResult.Refused(TemplateErrorCodes.RENDERER_UNAVAILABLE);
        }
        catch (InvalidDataException)
        {
            return AssetCheckResult.Refused(TemplateErrorCodes.FONT_NOT_WOFF2, ("found", "damaged"));
        }

        if (em < MIN_EM || em > MAX_EM)
        {
            var tooWide = em > MAX_EM;
            return AssetCheckResult.Refused(
                TemplateErrorCodes.FONT_WIDTH_OUT_OF_RANGE,
                ("problem", tooWide ? "too wide" : "too narrow"),
                ("width", FontMeasure.Format(em)),
                ("limit", FontMeasure.Format(tooWide ? MAX_EM : MIN_EM)));
        }
        return AssetCheckResult.Accepted(AssetContentTypes.WOFF2, $"{{\"avgCharEm\":{FontMeasure.Format(em)}}}");
    }

    private static string Describe(ReadOnlySpan<byte> head)
    {
        if (head.SequenceEqual(TRUE_TYPE_NUMBER) || head.SequenceEqual(TRUE_TYPE_NAME))
        {
            return "TrueType";
        }
        if (head.SequenceEqual(OPEN_TYPE))
        {
            return "OpenType";
        }
        return head.SequenceEqual(WOFF1) ? "woff 1" : "not a font";
    }
}
