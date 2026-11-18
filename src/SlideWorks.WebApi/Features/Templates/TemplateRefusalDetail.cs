namespace SlideWorks.WebApi.Features.Templates;

// One problem inside a refusal, such as one finding of a file check: where it is, its code (TemplateErrorCodes) and the
// text for a person.
public sealed record TemplateRefusalDetail(string Path, string Code, string Message);
