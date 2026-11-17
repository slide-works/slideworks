using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Templates.Views;

// The body of a refused template action: { "error": code, "message": text, "path"?, "errors": [{ path, code, message }] }.
// Path is left out when the refusal has none; Errors is always a list.
public sealed record TemplateErrorResponse(
    string Error,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path,
    IReadOnlyList<TemplateRefusalDetail> Errors);
