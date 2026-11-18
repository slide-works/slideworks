using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// "Update my decks" (D14) on the real service, through POST /api/templates/{id}/update-my-decks. Template rows come from
// TemplateRowSeed; decks are made through the route and pointed at the template by writing their row.
[TestFixture]
public class UpdateMyDecksTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";

    [Test]
    public async Task Update_WhenIOwnThreeDecks_ShouldMoveThemAndLogEach()
    {
        var template = await PublishedTwiceAsync();
        var decks = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            decks.Add(await DeckOnAsync(Client, template, 1));
        }
        var revisions = await RevisionsAsync(Client, decks);

        var count = await RunAsync(Client, template);

        count.ShouldBe(3);
        foreach (var deck in decks)
        {
            (await RowAsync(deck)).TemplateVersion.ShouldBe(2);
            (await SummariesAsync(Client, deck))[0].ShouldBe("Look: Seeded template version 1 to 2");
            (await SummariesAsync(Client, deck)).Count.ShouldBe(2);
        }
        (await RevisionsAsync(Client, decks)).ShouldBe([.. revisions.Select(r => r + 1)]);
    }

    [Test]
    public async Task Update_WhenADeckIsSomeoneElses_ShouldLeaveIt()
    {
        var template = await PublishedTwiceAsync();
        using var alex = ClientFor(TestUser.Alex);
        var mine = await DeckOnAsync(Client, template, 1);
        var theirs = await DeckOnAsync(alex, template, 1);
        var other = await DeckOnAsync(Client, "workshop", null);
        var before = await RevisionsAsync(alex, [theirs]);

        var count = await RunAsync(Client, template);

        count.ShouldBe(1);
        (await RowAsync(mine)).TemplateVersion.ShouldBe(2);
        (await RowAsync(theirs)).TemplateVersion.ShouldBe(1);
        (await RevisionsAsync(alex, [theirs])).ShouldBe(before);
        (await SummariesAsync(alex, theirs)).Count.ShouldBe(1);
        (await RowAsync(other)).Template.ShouldBe("workshop");
        (await SummariesAsync(Client, other)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Update_WhenADeckIsAlreadyOnTheLatest_ShouldNotLogIt()
    {
        var template = await PublishedTwiceAsync();
        var current = await DeckOnAsync(Client, template, 2);
        var stale = await DeckOnAsync(Client, template, 1);
        var before = await RevisionsAsync(Client, [current]);

        var count = await RunAsync(Client, template);

        count.ShouldBe(1);
        (await RevisionsAsync(Client, [current])).ShouldBe(before);
        (await SummariesAsync(Client, current)).Count.ShouldBe(1);
        (await SummariesAsync(Client, stale))[0].ShouldBe("Look: Seeded template version 1 to 2");
    }

    [Test]
    public async Task Update_WhenNotTheOwner_ShouldChangeNothing()
    {
        var template = await PublishedTwiceAsync(DeckSharing.Link);
        using var alex = ClientFor(TestUser.Alex);
        var theirs = await DeckOnAsync(alex, template, 1);
        var before = await RevisionsAsync(alex, [theirs]);

        using var asAlex = await alex.PostAsync(Route(template), null);
        using var unknown = await Client.PostAsync(Route("tpl_nosuchthing1"), null);
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var signedOut = await anonymous.PostAsync(Route(template), null);

        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        signedOut.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await asAlex.Content.ReadAsStringAsync()).ShouldBe((await unknown.Content.ReadAsStringAsync()).Replace("tpl_nosuchthing1", template, StringComparison.Ordinal));
        (await RowAsync(theirs)).TemplateVersion.ShouldBe(1);
        (await RevisionsAsync(alex, [theirs])).ShouldBe(before);
        (await SummariesAsync(alex, theirs)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Update_WhenOnlyADraftExists_ShouldMoveNothing()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var deck = await DeckOnAsync(Client, template, 1);

        var count = await RunAsync(Client, template);

        count.ShouldBe(0);
        (await RowAsync(deck)).TemplateVersion.ShouldBe(1);
        (await SummariesAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Update_WhenTheVersionLocksAPalette_ShouldSwitchThePalette()
    {
        var locking = await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), "..", "template-manifest.sample.json"));
        var template = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, MANIFEST);
        await AddVersionAsync(template, 2, locking);
        var deck = await DeckOnAsync(Client, template, 1, "ember");

        var count = await RunAsync(Client, template);

        count.ShouldBe(1);
        var row = await RowAsync(deck);
        row.Palette.ShouldBe("template");
        row.TemplateVersion.ShouldBe(2);
    }

    [Test]
    public async Task Update_WhenTheVersionDoesNotLockAPalette_ShouldKeepThePalette()
    {
        var template = await PublishedTwiceAsync();
        var deck = await DeckOnAsync(Client, template, 1, "ember");

        await RunAsync(Client, template);

        (await RowAsync(deck)).Palette.ShouldBe("ember");
    }

    [Test]
    public async Task Update_WhenTheTemplateIsArchived_ShouldStillMoveMyDecks()
    {
        var template = await PublishedTwiceAsync();
        var deck = await DeckOnAsync(Client, template, 1);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Templates.Where(t => t.Id == template).ExecuteUpdateAsync(s => s.SetProperty(t => t.ArchivedAt, scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow()));
        }

        var count = await RunAsync(Client, template);

        count.ShouldBe(1);
        (await RowAsync(deck)).TemplateVersion.ShouldBe(2);
    }

    [Test]
    public async Task Update_WhenADeckIsBeingPresented_ShouldTellItsViewersOnce()
    {
        var template = await PublishedTwiceAsync();
        var deck = await DeckOnAsync(Client, template, 1);
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await RunAsync(Client, template);

        (await received.Reader.ReadAsync()).ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        received.Reader.TryRead(out _).ShouldBeFalse();
    }

    private static string Route(string template) => $"/api/templates/{template}/update-my-decks";

    private async Task<string> PublishedTwiceAsync(DeckSharing access = DeckSharing.Private)
    {
        var template = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, MANIFEST, access: access);
        await AddVersionAsync(template, 2, MANIFEST);
        return template;
    }

    private async Task AddVersionAsync(string template, int version, string manifest)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TemplateVersions.Add(new Features.Templates.Data.TemplateVersion
        {
            TemplateId = template,
            Version = version,
            ManifestJson = manifest,
            PublishedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    private static async Task<int> RunAsync(HttpClient client, string template)
    {
        using var response = await client.PostAsync(Route(template), null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32();
    }

    // A deck made through the public route, then pointed at the template (the seam does not read the database yet).
    private async Task<string> DeckOnAsync(HttpClient owner, string template, int? version, string? palette = null)
    {
        var deck = await SharingApi.CreateDeckAsync(owner);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var moved = await db.Decks.Where(d => d.Id == deck).ExecuteUpdateAsync(set => set
            .SetProperty(d => d.Template, template)
            .SetProperty(d => d.TemplateVersion, version)
            .SetProperty(d => d.Palette, d => palette ?? d.Palette));
        moved.ShouldBe(1);
        return deck;
    }

    private async Task<Deck> RowAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.AsNoTracking().SingleAsync(d => d.Id == deck);
    }

    private static async Task<List<long>> RevisionsAsync(HttpClient owner, IEnumerable<string> decks)
    {
        var revisions = new List<long>();
        foreach (var deck in decks)
        {
            revisions.Add((await owner.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}")).GetProperty("revision").GetInt64());
        }
        return revisions;
    }

    private static async Task<IReadOnlyList<string>> SummariesAsync(HttpClient owner, string deck)
        => (await DeckSeed.ActivityAsync(owner, deck)).Select(e => e.GetProperty("summary").GetString()!).ToList();
}
