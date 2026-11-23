using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing;

// The PUT /api/decks/{id}/sharing body fails model binding when its level isn't one of the wire names (the enum converter
// takes no others) or its viewers aren't a list of strings. [ApiController] would answer that with a ProblemDetails; K4
// answers every error as { "error": "…" }, with the same text set_sharing gives. So this runs before that filter
// (ModelStateInvalidFilter, order -2000) and answers 400 with SharingService's messages, or BODY_INVALID for a body that
// is empty or isn't JSON at all.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SharingBodyErrorsAttribute : ActionFilterAttribute
{
    public const string BODY_INVALID = "body: expected JSON { level, viewers }";

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
        // A bad or missing field is reported under its path ($.level, $.viewers[0], Viewers) or, when a required one is
        // missing, in the message of the error at $. Anything else (an empty body, or one that isn't JSON) is the body's.
        var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).ToList();
        context.Result = new BadRequestObjectResult(new ErrorResponse(ErrorFor(errors)));
    }

    private static string ErrorFor(IReadOnlyList<KeyValuePair<string, ModelStateEntry?>> errors)
    {
        if (errors.Any(entry => entry.Value!.Errors.Any(e => e.ErrorMessage == ViewerEmails.TOO_MANY || e.Exception?.Message == ViewerEmails.TOO_MANY)))
        {
            return ViewerEmails.TOO_MANY;
        }
        if (errors.Any(entry => Mentions(entry, "level")))
        {
            return SharingService.LEVEL_INVALID;
        }
        if (errors.Any(entry => Mentions(entry, "viewers")))
        {
            return SharingService.VIEWERS_INVALID;
        }
        return errors.Any(entry => Mentions(entry, "people")) ? SharingService.PEOPLE_INVALID : BODY_INVALID;
    }

    private static bool Mentions(KeyValuePair<string, ModelStateEntry?> entry, string field)
        => Contains(entry.Key, field) || entry.Value!.Errors.Any(e => Contains(e.ErrorMessage, field) || Contains(e.Exception?.Message, field));

    private static bool Contains(string? text, string field) => text?.Contains(field, StringComparison.OrdinalIgnoreCase) == true;
}
