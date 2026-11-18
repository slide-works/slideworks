using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Render;

// The font measure for real (D8): the running app's engine sets the sample line in the fixture fonts in headless
// Chromium, and FontMeasure puts the width on the scale where the built-in body font is 0.56 em.
[TestFixture]
public class FontMeasureEngineTests
{
    private static FontMeasure Measure => AcceptanceApp.Services.GetRequiredService<FontMeasure>();

    [Test]
    public async Task Measure_WhenTheNormalFixture_ShouldBe056()
    {
        var em = await Measure.MeasureAsync(FontFixtures.Normal(), CancellationToken.None);

        em.ShouldBe(0.56, 0.01);
    }

    [Test]
    public async Task Measure_WhenTheNormalFixture_ShouldMatchTheReference()
    {
        var raw = await AcceptanceApp.Services.GetRequiredService<IRenderEngine>().MeasureFontAsync(FontFixtures.Normal(), CancellationToken.None);

        raw.ShouldBe(FontMeasure.REFERENCE, 0.0005);
    }

    [Test]
    public async Task Measure_WhenTheWideFixture_ShouldBeAtLeast057()
    {
        var em = await Measure.MeasureAsync(FontFixtures.Wide(), CancellationToken.None);

        em.ShouldBeGreaterThanOrEqualTo(0.57);
    }

    [Test]
    public async Task Measure_WhenTheBytesAreNotAFont_ShouldRefuseAsUndecodable()
    {
        var bytes = "wOF2"u8.ToArray().Concat(new byte[200]).ToArray();

        await Should.ThrowAsync<InvalidDataException>(() => Measure.MeasureAsync(bytes, CancellationToken.None));
    }
}
