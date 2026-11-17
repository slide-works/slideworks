namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of POST /api/join/{code}/reactions (guest-questions.json "reaction.request", Q7): one of "thumbsUp", "heart",
// "laugh", "wow" or "clap" (ReactionEmoji). It is a string so an unknown one is a 400 "emoji: unknown", not a binding error.
public sealed record ReactionRequest(string? Emoji);
