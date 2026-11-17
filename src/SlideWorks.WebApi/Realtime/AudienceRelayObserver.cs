using Agoda.IoC.Core;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Realtime;

// SW-4's registry tells this about every accepted change to a presentation (P10); the feed publishes it to the guests.
[RegisterSingleton(For = typeof(IPresentationObserver), OfCollection = true)]
public sealed class AudienceRelayObserver(AudienceFeed feed) : IPresentationObserver
{
    public Task OnChangedAsync(PresentationState state) => feed.OnChangedAsync(state);
}
