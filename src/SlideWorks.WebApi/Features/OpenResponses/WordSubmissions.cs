using Agoda.IoC.Core;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A guest's view of a word-cloud slide and their words submit (word-cloud.json). A code with no session is 404
// no_session. A submit is checked against the round the phone saw and OpenTextRules before anything is written.
[RegisterPerRequest]
public class WordSubmissions(IAudienceSessions sessions)
{
    private static readonly GuestResponseView OPEN = new(
        OpenResponseKind.WordCloud,
        OpenResponsePhase.Open,
        "6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63",
        1,
        9,
        "One word for how this sprint felt?",
        true,
        12,
        ["Focused", "Calm"],
        null);

    public async Task<GuestResponseView> ViewAsync(string code, int slide, string? guestId)
    {
        _ = await sessions.FindAsync(code) ?? throw new NotFoundException(GuestViews.NO_SESSION);
        // TODO: read the slide's question and its round in the session from the rows, with the guest's own words.
        return OPEN;
    }

    public async Task<StoredWords> SubmitAsync(string code, int slide, string guestId, WordsRequest request)
    {
        var view = await ViewAsync(code, slide, guestId);
        OpenRoundChecks.RequireCurrent(view, request.Round, request.Revision);
        OpenRoundChecks.RequireValid(OpenTextRules.Check(request.Words));
        // TODO: replace the guest's entries in the round with these words and bump its revision, in one transaction.
        return new StoredWords(OpenTextRules.WordsOf(request.Words));
    }
}
