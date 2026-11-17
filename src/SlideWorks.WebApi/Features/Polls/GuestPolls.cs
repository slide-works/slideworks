using System.Text.Json;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// The guest poll routes' checks over IPollAnswers (V5): a request with no sw_guest cookie is 400 no_guest before anything is
// read or written, then a malformed answer body is 400 (AnswerBody), and a code with no session is 404 no_session, as on
// GET /api/join/{code}.
[RegisterPerRequest]
public class GuestPolls(IPollAnswers answers)
{
    public const string NO_GUEST = "no_guest";

    public async Task<GuestPollView> ViewAsync(string code, string? guestId)
        => await answers.ViewAsync(code, Require(guestId)) ?? throw new NotFoundException(GuestViews.NO_SESSION);

    public Task<AnswerResult> SubmitAsync(string code, string? guestId, JsonElement body)
    {
        var guest = Require(guestId);
        return answers.SubmitAsync(code, guest, AnswerBody.Read(body));
    }

    private static string Require(string? guestId) => guestId ?? throw new BadRequestException(NO_GUEST);
}
