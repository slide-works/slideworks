using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates;

// How every template service refuses: the code (TemplateErrorCodes) with its frozen text from template-errors.json,
// the text's placeholders filled from values. An unknown code or a placeholder without a value throws ArgumentException,
// so a refusal can never carry a text nobody wrote.
public static class TemplateRefusal
{
    public static TemplateRefusedException Of(string code, string? path = null, params (string Name, string Value)[] values)
        => new(code, TemplateErrorTexts.Format(code, values), path);

    // The same with a file check's findings, which reach the caller as the body's "errors".
    public static TemplateRefusedException Of(
        string code, IReadOnlyList<TemplateRefusalDetail> errors, string? path = null, params (string Name, string Value)[] values)
        => new(code, TemplateErrorTexts.Format(code, values), path) { Errors = errors };
}
