namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// One word of a cloud (audience-events.json "wordCloud"): the first casing anyone sent, how many guests hold it now, and
// whether the presenter hid it.
public record CloudWord(string Text, int Count, bool Hidden);
