using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Templates;

// What every ITemplates answers about the built-ins (T4), whatever it is made of. Each implementation has a subclass
// that makes it in Create.
public abstract class TemplatesContractTests
{
    protected static readonly TemplateCatalogue BuiltIns = new();

    protected abstract ITemplates Create();

    [Test]
    public async Task Find_WhenTheNameIsBuiltIn_ShouldResolveIt()
    {
        var workshop = BuiltIns.All.First(t => !t.IsDefault);

        var found = await Create().FindAsync(workshop.Name, null, CancellationToken.None);

        found.ShouldNotBeNull();
        found.Template.ShouldBe(workshop);
        found.Ref.ShouldBe(workshop.Name);
        found.Version.ShouldBeNull();
        found.LatestVersion.ShouldBeNull();
        found.BuiltIn.ShouldBeTrue();
        found.OwnPalette.ShouldBeNull();
        found.Look.ShouldBe(TemplateLook.OfBuiltIn(workshop));
    }

    // A built-in has no versions: whatever version is asked, and a deck stores none or a stale one, it is found.
    [Test]
    public async Task Find_WhenABuiltInIsAskedWithAVersion_ShouldIgnoreTheVersion()
    {
        var workshop = BuiltIns.All.First(t => !t.IsDefault);
        var templates = Create();

        var found = await templates.FindAsync(workshop.Name, 7, CancellationToken.None);
        var forDeck = await templates.ForDeckAsync(DeckOn(workshop.Name, 7), CancellationToken.None);

        found.ShouldNotBeNull();
        found.Template.ShouldBe(workshop);
        found.Version.ShouldBeNull();
        found.BuiltIn.ShouldBeTrue();
        forDeck.Template.ShouldBe(workshop);
    }

    // Only ForDeckAsync falls back to the default; FindAsync says there is no such template.
    [TestCase(null)]
    [TestCase(1)]
    public async Task Find_WhenTheUploadedRefIsUnknown_ShouldAnswerNull(int? version)
    {
        var found = await Create().FindAsync("tpl_nosuchtemplate", version, CancellationToken.None);

        found.ShouldBeNull();
    }

    [Test]
    public async Task ForDeck_WhenTheRefIsUnknown_ShouldFallBackToTheDefault()
    {
        var found = await Create().ForDeckAsync(DeckOn("no-such-template", null), CancellationToken.None);

        found.Template.ShouldBe(BuiltIns.Default);
        found.Ref.ShouldBe(BuiltIns.Default.Name);
        found.BuiltIn.ShouldBeTrue();
    }

    [Test]
    public async Task ListFor_WhenSignedIn_ShouldListTheBuiltIns()
    {
        var listed = await Create().ListForAsync(SignedIn("ada@example.test"), CancellationToken.None);

        var builtIns = listed.Where(t => t.BuiltIn).ToList();
        builtIns.Select(t => t.Ref).ShouldBe(BuiltIns.All.Select(t => t.Name), ignoreOrder: true);
        builtIns.ShouldAllBe(t => t.Version == null && !t.Archived);
    }

    protected static Deck DeckOn(string template, int? version)
        => new()
        {
            Id = "deckdeckdeck",
            Slug = "a-deck",
            Title = "A deck",
            PartsJson = "[]",
            Template = template,
            TemplateVersion = version,
            Palette = "cobalt",
        };

    protected static ICurrentActor SignedIn(string email) => new Actor(Guid.NewGuid(), email);

    private sealed record Actor(Guid? PresenterId, string? Email) : ICurrentActor
    {
        public bool IsSignedIn => PresenterId != null;

        public string? Name => null;

        public ActorKind Kind => ActorKind.Person;

        public ActorVia Via => ActorVia.Web;

        public UserAccess? Access => UserAccess.Approved;

        public bool IsAdmin => false;
    }
}
