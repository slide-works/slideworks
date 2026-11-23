namespace SlideWorks.WebApi.Tests.Support;

// The client's fixture image, a real PNG, read by path from the repo the way contract examples are.
public static class SampleImage
{
    public static byte[] Png()
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(JsonShape.ScreensDirectory()))!;
        return File.ReadAllBytes(Path.Combine(root, "SlideWorks.Client", "src", "test-support", "fixtures", "sample-image.png"));
    }
}
