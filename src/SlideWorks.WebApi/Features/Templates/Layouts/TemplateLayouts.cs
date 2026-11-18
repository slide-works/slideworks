using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

[RegisterPerRequest(For = typeof(ITemplateLayouts))]
public sealed class TemplateLayouts : ITemplateLayouts
{
    // TODO: no custom layouts until the store is built; the real body reads the version's manifest and its layout assets.
    public Task<IReadOnlyList<TemplateLayout>> ForVersionAsync(string templateId, int version, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TemplateLayout>>([]);

    // TODO: no custom layouts until the store is built; the real body checks the report against the draft's layout and
    // writes the approval.
    public Task<ApproveOutcome> ApproveAsync(string templateId, string name, LayoutReport report, CancellationToken ct)
        => Task.FromResult(ApproveOutcome.NotFound);
}
