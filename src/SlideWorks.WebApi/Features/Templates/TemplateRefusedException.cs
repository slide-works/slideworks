namespace SlideWorks.WebApi.Features.Templates;

// Thrown by every template service when it refuses an action; the action has changed nothing. Code is one of
// TemplateErrorCodes, Path is the manifest field or file the refusal is about (null when it is about the whole
// template), and Errors holds a file check's findings (empty otherwise). TemplateErrorsAttribute answers it as 400, or
// 409 for stale_revision; McpToolErrors.Run reports it as a tool error.
public sealed class TemplateRefusedException(string code, string message, string? path = null) : Exception(message)
{
    public string Code { get; } = code;

    public string? Path { get; } = path;

    public IReadOnlyList<TemplateRefusalDetail> Errors { get; init; } = [];
}
