namespace SlideWorks.WebApi.Features.Templates.Views;

// GET /api/templates: the templates page (src/contracts/screens/templates.json).
public sealed record TemplatesView(IReadOnlyList<TemplateRowView> Mine, IReadOnlyList<TemplateRowView> Shared, IReadOnlyList<TemplateRowView> BuiltIn);
