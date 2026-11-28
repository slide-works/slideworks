namespace SlideWorks.WebApi.Realtime;

// The language a guest's state is composed in (SW-883 K5): sent as audienceState.language, null while translations are off.
// FallbackLines is the slide on screen's missing plus stale lines, which the phone shows in the primary language.
public sealed record AudienceLanguage(string Code, int FallbackLines);
