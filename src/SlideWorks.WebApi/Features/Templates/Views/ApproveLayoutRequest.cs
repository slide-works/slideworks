namespace SlideWorks.WebApi.Features.Templates.Views;

// POST /api/templates/{id}/layouts/{name}/approve: the layout file the owner saw, and the overflow and contrast
// findings they accept.
public sealed record ApproveLayoutRequest(string Asset, IReadOnlyList<string> Overflow, IReadOnlyList<string> Contrast);
