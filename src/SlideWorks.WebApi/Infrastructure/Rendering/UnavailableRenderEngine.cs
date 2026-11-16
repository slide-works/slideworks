using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// TODO: replace with the Playwright engine that renders the SPA's card routes in the image's Chromium.
[RegisterSingleton(For = typeof(IRenderEngine))]
public sealed class UnavailableRenderEngine : IRenderEngine
{
    public const string MESSAGE = "Card rendering isn't available yet";

    public Task<byte[]> RenderCardAsync(string deckId, CancellationToken ct) => throw new RendererUnavailableException(MESSAGE);

    public Task<byte[]> RenderGenericCardAsync(CancellationToken ct) => throw new RendererUnavailableException(MESSAGE);
}
