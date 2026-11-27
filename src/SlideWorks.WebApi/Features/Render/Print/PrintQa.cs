namespace SlideWorks.WebApi.Features.Render.Print;

// The newest Q&A round of the newest session (deck-print.json "qa"). Phase is "closed" once the round has been closed,
// else "open". Total counts the open and answered questions; Top is the five with most votes, ties to the lower number.
public record PrintQa(string Phase, int Total, IReadOnlyList<PrintQaQuestion> Top);
