namespace SlideWorks.WebApi.Features.Templates.Views;

// POST /api/templates: BasedOn is the template the draft starts from, Name the new template's label.
public sealed record CreateTemplateRequest(string BasedOn, string Name);
