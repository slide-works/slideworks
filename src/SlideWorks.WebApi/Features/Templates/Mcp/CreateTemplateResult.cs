namespace SlideWorks.WebApi.Features.Templates.Mcp;

// create_template: Template is the new draft's tpl_ id, Url the editor's address, Checks the publish checklist.
public sealed record CreateTemplateResult(string Template, string Status, int Revision, string Url, IReadOnlyList<TemplateCheck> Checks);
