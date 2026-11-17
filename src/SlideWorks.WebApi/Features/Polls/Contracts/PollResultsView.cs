namespace SlideWorks.WebApi.Features.Polls.Contracts;

// What get_poll_results returns (poll-results.json, V9): the poll's question and its current options, and each audience
// session's rounds that were opened (each with the option texts it ran with). Totals only, never who picked what (D5).
public sealed record PollResultsView(string Question, IReadOnlyList<string> Options, IReadOnlyList<PollResultsSession> Sessions);
