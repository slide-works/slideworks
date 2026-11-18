namespace SlideWorks.WebApi.Features.Templates.Layouts;

// The custom layouts of a template (T10), for the code that draws and validates slides and for the review page.
public interface ITemplateLayouts
{
    // The layouts of one version, in the manifest's order; version 0 is the draft. An entry whose asset is missing or is
    // not a layout is left out. Approved or not, every layout is here: the caller skips the unapproved ones.
    Task<IReadOnlyList<TemplateLayout>> ForVersionAsync(string templateId, int version, CancellationToken ct);

    // Approves the draft's layout of that name with what the review page measured. Anything but Approved writes nothing.
    // Who may approve is the caller's check, not this seam's.
    Task<ApproveOutcome> ApproveAsync(string templateId, string name, LayoutReport report, CancellationToken ct);
}
