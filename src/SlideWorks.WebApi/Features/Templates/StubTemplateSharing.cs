using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// TODO: returns the screen sample until the real service lands.
[RegisterPerRequest(For = typeof(ITemplateSharing))]
public sealed class StubTemplateSharing : ITemplateSharing
{
    public Task<TemplateSharingView> GetAsync(ICurrentActor actor, string id) => Task.FromResult(TemplateSamples.Read<TemplateSharingView>(TemplateSamples.SHARING));

    public Task<TemplateSharingView> SetAsync(ICurrentActor actor, string id, TemplateSharingUpdate update) => GetAsync(actor, id);

    public Task<TemplatePublicView> PublicAsync(ICurrentActor actor, string id) => Task.FromResult(TemplateSamples.Read<TemplatePublicView>(TemplateSamples.PUBLIC));

    public Task UseAsync(ICurrentActor actor, string id) => Task.CompletedTask;
}
