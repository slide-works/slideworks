using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// A template as one .zip file (D4): template.json plus the files it names. Import makes a draft from a bundle, export
// writes a template's draft out as one.
public interface ITemplateBundles
{
    // Makes a new draft template, owned by the actor, from the bundle read from zip. Returns the draft's "tpl_…" id.
    Task<string> ImportAsync(ICurrentActor actor, Stream zip, CancellationToken ct);

    // The template's bundle, for its owner only. The caller disposes the file's Content.
    Task<TemplateBundleFile> ExportAsync(ICurrentActor actor, string id, CancellationToken ct);
}
