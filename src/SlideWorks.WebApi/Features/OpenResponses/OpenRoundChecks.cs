using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The checks a guest's submit passes before anything is written: the round it was typed against is the open one and the
// phone saw its latest revision, and the text keeps OpenTextRules. A profane submit is 409; any other rule is 400.
public static class OpenRoundChecks
{
    public const string STALE = "stale";
    public const string WRONG_PHASE = "wrong_phase";

    public static void RequireCurrent(GuestResponseView view, string round, int revision)
    {
        if (view.Phase != OpenResponsePhase.Open)
        {
            throw new ConflictException(WRONG_PHASE);
        }
        if (view.Round != round || view.Revision != revision)
        {
            throw new ConflictException(STALE);
        }
    }

    public static void RequireValid(string? broken)
    {
        if (broken == OpenTextRules.PROFANE)
        {
            throw new ConflictException(broken);
        }
        if (broken is not null)
        {
            throw new BadRequestException(broken);
        }
    }
}
