using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// TODO: returns the screen sample until the real service lands.
[RegisterPerRequest(For = typeof(ITemplateDrafts))]
public sealed class StubTemplateDrafts : ITemplateDrafts
{
    public Task<TemplatesView> ListAsync(ICurrentActor actor) => Task.FromResult(TemplateSamples.Read<TemplatesView>(TemplateSamples.TEMPLATES));

    public Task<TemplateCreated> CreateAsync(ICurrentActor actor, string basedOn, string name) => Task.FromResult(new TemplateCreated("tpl_Nw4kT8pQz2Lm"));

    public Task<TemplateEditorView> GetAsync(ICurrentActor actor, string id) => Task.FromResult(TemplateSamples.Read<TemplateEditorView>(TemplateSamples.EDITOR));

    public Task<TemplateEditorView> UpdateAsync(ICurrentActor actor, string id, string manifestJson, int revision) => GetAsync(actor, id);
}
