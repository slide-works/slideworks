using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Render.Print;

// One poll slide's newest opened round in the newest session (deck-print.json "polls"): the client's PollScreen plus the
// slide. Counts are always filled, since only a person who can present the deck downloads the PDF. Max is null unless
// Pick is upTo, Correct is null unless AnswerShown. Replies and Words are what the presenter's PollControlView holds for
// the round, with a null list sent as an empty one.
public record PrintPoll(
    int Slide,
    PollPhase Phase,
    int Number,
    int Answered,
    IReadOnlyList<int> Counts,
    bool AnswerShown,
    int? Correct,
    PollPick Pick,
    int? Max,
    PollAnswersForm Answers,
    IReadOnlyList<string> Replies,
    IReadOnlyList<PollWord> Words);
