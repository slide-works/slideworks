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
