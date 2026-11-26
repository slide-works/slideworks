namespace SlideWorks.WebApi.Features.Render.Print;

// One of the questions the Q&A slide prints (deck-print.json "qa.top"): its stable number, its text and its votes.
public record PrintQaQuestion(int Number, string Text, int Votes);
