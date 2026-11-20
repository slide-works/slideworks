namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// The 200 body of an answer submit (open-text.json "stored"): the guest's answer as stored, collapsed.
public record StoredText(string Text);
