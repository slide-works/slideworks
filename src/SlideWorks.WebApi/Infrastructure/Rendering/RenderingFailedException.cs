namespace SlideWorks.WebApi.Infrastructure.Rendering;

// A render that ran and failed (D3): the page reported data-error, an image failed to load, or the page itself failed.
// Its message is "Rendering failed: <text>" and never carries Playwright's message, which holds the render token.
// An InvalidOperationException, so a caller written against D3's first wording still catches it.
public sealed class RenderingFailedException(string text) : InvalidOperationException(PlaywrightRenderEngine.FAILED + text)
{
    // The text after "Rendering failed: ".
    public string Reason { get; } = text;
}
