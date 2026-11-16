using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Sharing.Views;

namespace SlideWorks.WebApi.Tests.Contracts;

// What EnumWireProbeController sends: one record with DeckSharing and ActorKind, one with ActorKind and ActorVia.
public sealed record EnumWireSample(SharingView Sharing, ActivityRow Activity);
