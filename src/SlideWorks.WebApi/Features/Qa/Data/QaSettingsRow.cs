using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Data;

// A deck's Q&A and reaction settings (Q1, D16), keyed by the deck. Made the first time a setting is changed; a deck with no
// row has QaSettings.Default. Changed in place, never deleted. Only Features/Qa writes it.
public class QaSettingsRow
{
    public required string DeckId { get; set; }

    public QaQuestions Questions { get; set; }

    public bool Review { get; set; }

    public bool Names { get; set; }

    public bool Reactions { get; set; }

    public QaShowOn ShowOn { get; set; }

    public static QaSettingsRow From(string deckId, QaSettings settings) => new()
    {
        DeckId = deckId,
        Questions = settings.Questions,
        Review = settings.Review,
        Names = settings.Names,
        Reactions = settings.Reactions,
        ShowOn = settings.ShowOn,
    };

    public QaSettings ToSettings() => new(Questions, Review, Names, Reactions, ShowOn);
}
