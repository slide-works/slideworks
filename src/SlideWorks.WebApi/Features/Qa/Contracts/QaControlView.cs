using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// What the presenter's Q&A controls show (qa-control.json, Q6): every Q&A route's response and the questionControl event's
// payload. Seq only increases (QaSeq), so a late view is dropped. Round is the Q&A round id, null when not live or with
// questions off. Here is the switch of the presenter's current slide, null when not live. Slides lists every slide with
// something switched off; a slide not in it has both on. OnScreen is the number of the question on screen, or null.
// Reactions is the session's totals per emoji, counts not yet written included. Not live: Live false, Round null, no
// questions and zero counts. Every number is an integer.
public sealed record QaControlView(
    long Seq,
    bool Live,
    string? Round,
    QaSettings Settings,
    QaSlideView? Here,
    IReadOnlyList<QaSlideView> Slides,
    QaCounts Counts,
    int? OnScreen,
    IReadOnlyList<QaQuestion> Questions,
    ReactionCounts Reactions);
