namespace SlideWorks.WebApi.Features.Templates.Mcp;

// update_template: the draft's new revision and its publish checklist.
public sealed record UpdateTemplateResult(string Template, int Revision, IReadOnlyList<TemplateCheck> Checks);
