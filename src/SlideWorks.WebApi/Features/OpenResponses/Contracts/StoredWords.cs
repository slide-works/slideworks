namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// The 200 body of a words submit (word-cloud.json "stored"): the guest's words as stored, collapsed and one per key.
public record StoredWords(IReadOnlyList<string> Words);
