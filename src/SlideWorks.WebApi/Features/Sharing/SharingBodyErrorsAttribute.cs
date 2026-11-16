using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing;

// The PUT /api/decks/{id}/sharing body fails model binding when its level isn't one of the wire names (the enum converter
// takes no others) or its viewers aren't a list of strings. [ApiController] would answer that with a ProblemDetails; K4
// answers every error as { "error": "…" }, with the same text set_sharing gives. So this runs before that filter
// (ModelStateInvalidFilter, order -2000) and answers 400 with SharingService's messages.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SharingBodyErrorsAttribute : ActionFilterAttribute
{
    private const int BEFORE_MODEL_STATE_INVALID_FILTER = -3000;

    public SharingBodyErrorsAttribute()
    {
        Order = BEFORE_MODEL_STATE_INVALID_FILTER;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }
        // A bad or missing level is reported under its path ($.level) or, when missing, in the message of the error at $.
        var aboutLevel = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .Any(entry => MentionsLevel(entry.Key) || entry.Value!.Errors.Any(e => MentionsLevel(e.ErrorMessage) || MentionsLevel(e.Exception?.Message)));
        var error = aboutLevel ? SharingService.LEVEL_INVALID : SharingService.VIEWERS_INVALID;
        context.Result = new BadRequestObjectResult(new ErrorResponse(error));
    }

    private static bool MentionsLevel(string? text) => text?.Contains("level", StringComparison.OrdinalIgnoreCase) == true;
}
