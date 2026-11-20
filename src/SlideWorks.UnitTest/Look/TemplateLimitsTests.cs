using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.UnitTest.Look;

// What one template may store over its whole life (T6): its files are never removed, so the room is larger than a bundle.
public class TemplateLimitsTests
{
    [Test]
    public void MaxStored_WhenRead_ShouldBeFourBundlesWorth()
    {
        TemplateLimits.MAX_STORED_ASSETS.ShouldBe(256);
        TemplateLimits.MAX_STORED_ASSETS.ShouldBe(4 * TemplateLimits.MAX_BUNDLE_ENTRIES);
        TemplateLimits.MAX_STORED_BYTES.ShouldBe(240_000_000L);
        TemplateLimits.MAX_STORED_BYTES.ShouldBeGreaterThanOrEqualTo(TemplateLimits.MAX_BUNDLE_BYTES + TemplateLimits.MAX_IMAGE_BYTES);
        TemplateLimits.MAX_STORED_BYTES.ShouldBeLessThanOrEqualTo(4L * TemplateLimits.MAX_BUNDLE_BYTES);
    }
}
