using Agoda.IoC.Core;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A guest's view of an open-text slide and their answer submit (open-text.json). A code with no session is 404
// no_session. A submit is checked against the round the phone saw and OpenTextRules before anything is written.
[RegisterPerRequest]
public class OpenAnswers(IAudienceSessions sessions)
{
    private static readonly GuestResponseView OPEN = new(
        OpenResponseKind.OpenText,
        OpenResponsePhase.Open,
        "a3d81c5e-7f2b-4e96-b0c4-1d8e6f9a2b57",
        1,
        6,
        "What would help your team ship smaller changes?",
        true,
        4,
        null,
        "A shared definition of done");

    public async Task<GuestResponseView> ViewAsync(string code, int slide, string? guestId)
    {
        _ = await sessions.FindAsync(code) ?? throw new NotFoundException(GuestViews.NO_SESSION);
        // TODO: read the slide's question and its round in the session from the rows, with the guest's own answer.
        return OPEN;
    }

    public async Task<StoredText> SubmitAsync(string code, int slide, string guestId, TextRequest request)
    {
        var view = await ViewAsync(code, slide, guestId);
        OpenRoundChecks.RequireCurrent(view, request.Round, request.Revision);
        OpenRoundChecks.RequireValid(OpenTextRules.Check(request.Text));
        // TODO: replace the guest's answer in the round and bump its revision, in one transaction.
        return new StoredText(OpenTextRules.Collapse(request.Text));
    }
}
