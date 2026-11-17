namespace SlideWorks.WebApi.Features.Templates.Views;

// GET /api/templates/{id}/checklist: the editor view's checklist and canPublish on their own.
public sealed record TemplateChecklistView(IReadOnlyList<TemplateChecklistRow> Checklist, bool CanPublish);
