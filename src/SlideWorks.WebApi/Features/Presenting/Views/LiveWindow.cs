using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Presenting.Views;

// One window or phone connected to a live presentation, as the phone's pick page lists it (presentations.json, P4).
public sealed record LiveWindow(PresentationRole Role, string Label);
