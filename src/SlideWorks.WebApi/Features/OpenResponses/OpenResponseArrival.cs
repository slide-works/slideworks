using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Opens a word-cloud or open-text slide's round when the presenter arrives on it and closes it when they leave (D5), through
// OpenResponseControls.ArriveAsync. A singleton because it is an observer, so the per-request controls are resolved through
// a scope per call. One change is handled at a time, so two quick moves can't both open the same slide's first round. A
// failure goes back to the registry, which logs it and carries on.
[RegisterSingleton(For = typeof(IPresentationObserver), OfCollection = true)]
public sealed class OpenResponseArrival(IServiceScopeFactory scopes) : IPresentationObserver, IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);

    public async Task OnChangedAsync(PresentationState state)
    {
        await _turn.WaitAsync();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OpenResponseControls>().ArriveAsync(state.DeckId, state.Slide, CancellationToken.None);
        }
        finally
        {
            _turn.Release();
        }
    }

    public void Dispose() => _turn.Dispose();
}
