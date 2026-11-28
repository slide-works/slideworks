using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// The real IAudienceRelay (J4): guests follow the presenter through SW-4's presentation state, projected to the one slide
// on screen. All of it is AudienceFeed, which the presentation observer shares, so the two never keep separate seqs.
[RegisterSingleton(For = typeof(IAudienceRelay))]
public sealed class AudienceRelay(AudienceFeed feed) : IAudienceRelay
{
    public Task<AudienceState?> CurrentAsync(string deckId, string? language) => feed.CurrentAsync(deckId, language);

    public Task StartedAsync(string deckId) => feed.StartedAsync(deckId);

    public Task EndedAsync(string deckId) => feed.EndedAsync(deckId);

    public Task RefreshAsync(string deckId) => feed.RefreshAsync(deckId);
}
