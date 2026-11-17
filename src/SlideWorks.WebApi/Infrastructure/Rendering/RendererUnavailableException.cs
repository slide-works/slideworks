namespace SlideWorks.WebApi.Infrastructure.Rendering;

// No headless browser can render right now: none is installed, or it failed to start.
public sealed class RendererUnavailableException(string message) : Exception(message)
{
}
