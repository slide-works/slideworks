using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Tests.Templates;

// The seam on the real host (T4): both of its services resolve from a request's scope, with everything they depend on,
// so a per-request caller can take them.
[TestFixture]
public class TemplateSeamHostTests : ApiTestFixture
{
    [Test]
    public async Task Templates_WhenResolvedFromARequestScope_ShouldAnswerTheBuiltIns()
    {
        using var scope = Factory.Services.CreateScope();

        var templates = scope.ServiceProvider.GetRequiredService<ITemplates>();
        var talk = await templates.FindAsync("talk", null, CancellationToken.None);

        templates.ShouldBeOfType<SlideWorks.WebApi.Features.Templates.Templates>();
        scope.ServiceProvider.GetRequiredService<ITemplates>().ShouldBeSameAs(templates);
        talk.ShouldNotBeNull();
        talk.BuiltIn.ShouldBeTrue();
        talk.FitWidths.ShouldBe(FitWidths.Default);
    }

    [Test]
    public void DeckLooks_WhenResolvedFromARequestScope_ShouldBeOnePerScope()
    {
        using var scope = Factory.Services.CreateScope();
        using var other = Factory.Services.CreateScope();

        var looks = scope.ServiceProvider.GetRequiredService<IDeckLooks>();

        looks.ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IDeckLooks>().ShouldBeSameAs(looks);
        other.ServiceProvider.GetRequiredService<IDeckLooks>().ShouldNotBeSameAs(looks);
    }
}
