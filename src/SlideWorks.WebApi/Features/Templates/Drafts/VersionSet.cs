using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Drafts;

// The version rows of one template, lowest first. Current is what the editor shows: the draft, or the latest published
// version when no draft row exists. Published is the highest published number, 0 while there is none.
public sealed class VersionSet(IReadOnlyList<TemplateVersion> all)
{
    public IReadOnlyList<TemplateVersion> All => all;

    public TemplateVersion Current => all.FirstOrDefault(v => v.Version == TemplateVersion.DRAFT) ?? all[^1];

    public int Published => all.Count == 0 ? TemplateVersion.DRAFT : all[^1].Version;
}
