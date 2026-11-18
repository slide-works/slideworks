using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// The contract over the fake, with a template added so the built-ins are told apart from it, and what the fake adds.
[TestFixture]
public class FakeTemplatesContractTests : TemplatesContractTests
{
    private const string NORTHWIND = "tpl_northwind";

    protected override ITemplates Create()
    {
        var fake = new FakeTemplates();
        fake.Add(Uploaded(NORTHWIND, 1));
        return fake;
    }

    [Test]
    public async Task Find_WhenTwoVersionsWereAdded_ShouldAnswerTheHighest()
    {
        var fake = new FakeTemplates();
        fake.Add(Uploaded(NORTHWIND, 2));
        fake.Add(Uploaded(NORTHWIND, 1));

        var latest = await fake.FindAsync(NORTHWIND, null, CancellationToken.None);
        var first = await fake.FindAsync(NORTHWIND, 1, CancellationToken.None);
        var missing = await fake.FindAsync(NORTHWIND, 3, CancellationToken.None);

        latest.ShouldNotBeNull();
        latest.Version.ShouldBe(2);
        first.ShouldNotBeNull();
        first.Version.ShouldBe(1);
        missing.ShouldBeNull();
    }

    [Test]
    public async Task ListFor_WhenNotPickable_ShouldLeaveItOut()
    {
        var fake = new FakeTemplates();
        fake.Add(Uploaded("tpl_everyone", 1));
        fake.Add(Uploaded("tpl_ada", 1), "ada@example.test");
        fake.Add(Uploaded("tpl_grace", 1), "grace@example.test");
        fake.Add(Uploaded("tpl_archived", 1) with { Archived = true });

        var listed = await fake.ListForAsync(SignedIn("ada@example.test"), CancellationToken.None);

        listed.Where(t => !t.BuiltIn).Select(t => t.Ref).ShouldBe(["tpl_everyone", "tpl_ada"], ignoreOrder: true);
    }

    [Test]
    public async Task ForDeck_WhenABuiltInRefWasAdded_ShouldAnswerTheAddedOne()
    {
        var talk = BuiltIns.Default;
        var wider = BuiltInTemplates.Resolve(talk with { Layouts = [.. talk.Layouts, "wide-only"] });
        var fake = new FakeTemplates();
        fake.Add(wider);

        var found = await fake.ForDeckAsync(DeckOn(talk.Name, null), CancellationToken.None);
        var listed = await fake.ListForAsync(SignedIn("ada@example.test"), CancellationToken.None);

        found.ShouldBe(wider);
        found.Template.Allows("wide-only").ShouldBeTrue();
        listed.Count(t => t.Ref == talk.Name).ShouldBe(1);
    }

    // A deck on a built-in may store a version (none means anything there); the added template still answers.
    [Test]
    public async Task Find_WhenAnAddedBuiltInRefIsAskedWithAVersion_ShouldAnswerTheAddedOne()
    {
        var talk = BuiltIns.Default;
        var wider = BuiltInTemplates.Resolve(talk) with { FitWidths = new FitWidths(0.62, 0.62, 0.62) };
        var fake = new FakeTemplates();
        fake.Add(wider);

        var found = await fake.FindAsync(talk.Name, 3, CancellationToken.None);
        var forDeck = await fake.ForDeckAsync(DeckOn(talk.Name, 3), CancellationToken.None);

        found.ShouldBe(wider);
        forDeck.ShouldBe(wider);
        forDeck.FitWidths.Body.ShouldBe(0.62);
    }

    [Test]
    public async Task FindLayout_WhenTheNameIsBuiltIn_ShouldAnswerItsDefinition()
    {
        var found = await Create().FindAsync(NORTHWIND, null, CancellationToken.None);

        found.ShouldNotBeNull();
        found.FindLayout("title").ShouldBe(LayoutCatalogue.Find("title"));
        found.FindLayout("title").ShouldNotBeNull();
        found.FindLayout("no-such-layout").ShouldBeNull();
    }

    // The container answers the last registration of a service, so the fake must come after the real one.
    [Test]
    public void Register_WhenTheBuiltInsWereRegistered_ShouldRegisterTheFakeLast()
    {
        var services = new ServiceCollection().AddScoped<ITemplates, BuiltInTemplates>();

        FakeTemplates.Register(services);

        var last = services.Last(d => d.ServiceType == typeof(ITemplates));
        last.ImplementationFactory.ShouldNotBeNull();
        last.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        services.Count(d => d.ServiceType == typeof(FakeTemplates) && d.Lifetime == ServiceLifetime.Singleton).ShouldBe(1);
    }

    private static ResolvedTemplate Uploaded(string templateRef, int version)
        => new(BuiltIns.Default with { Name = templateRef, Label = "Northwind", IsDefault = false }, templateRef, version, false) { LatestVersion = version };
}
