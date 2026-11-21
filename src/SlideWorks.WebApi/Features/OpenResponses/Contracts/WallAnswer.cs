namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// One answer on the wall (audience-events.json "openText"): the guest's stable 1-based number, their text, and whether
// the presenter hid it.
public record WallAnswer(int Number, string Text, bool Hidden);
