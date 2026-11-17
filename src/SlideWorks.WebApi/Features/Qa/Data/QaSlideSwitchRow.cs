using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Data;

// One slide's Q&A switch (Q1, D10), keyed by the slide. Made the first time something is turned off on the slide. It is
// changed in place and never deleted, so a row whose two values are both true again means the slide follows the deck, as a
// slide with no row does. Only Features/Qa writes it.
public class QaSlideSwitchRow
{
    public Guid SlideId { get; set; }

    public bool Questions { get; set; }

    public bool Reactions { get; set; }

    public static QaSlideSwitchRow From(Guid slideId, QaSlideSwitch slideSwitch) => new()
    {
        SlideId = slideId,
        Questions = slideSwitch.Questions,
        Reactions = slideSwitch.Reactions,
    };

    public QaSlideSwitch ToSwitch() => new(Questions, Reactions);
}
