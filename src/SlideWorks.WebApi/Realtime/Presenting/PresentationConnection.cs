namespace SlideWorks.WebApi.Realtime.Presenting;

// One client in a presentation's group (P1). ClientId is the random id the page makes once per load, so a rejoin with the
// same id replaces this entry. Label comes from the client at join, trimmed and capped at 60 characters.
public sealed record PresentationConnection(string ClientId, PresentationRole Role, string Label);
