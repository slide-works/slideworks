using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Who may use a template (T5) on the real service: the owner's sharing routes, the template's public page, "Use this
// template" and set_sharing {template}. Template rows come from TemplateRowSeed. Sam owns every template; Alex and Mira
// are the people it is shared with or not. A template the caller may not see answers exactly as one that does not exist.
[TestFixture]
public class TemplateSharingTests : ApiTestFixture
{
    private string? _manifest;

    [Test]
    public async Task Share_WhenNamedPeople_ShouldStoreThem()
    {
        var id = await PublishedAsync();

        using var put = await Client.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "named", viewers = new[] { " Mira@Team.Example ", "alex@team.example", "ALEX@team.example", TestUser.Sam.Email } });
        var read = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}/sharing");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var view in new[] { await put.Content.ReadFromJsonAsync<JsonElement>(), read })
        {
            view.GetProperty("level").GetString().ShouldBe("named");
            view.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example", "mira@team.example"]);
            view.GetProperty("link").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/t/{id}");
        }
        (await ViewersAsync(id)).Order().ToList().ShouldBe(["alex@team.example", "mira@team.example"]);
    }

    [Test]
    public async Task Share_WhenViewersLeftOutOfTheAgentCall_ShouldKeepThem()
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { template = id, level = "link" });

        isError.ShouldBeFalse(result.ToString());
        result.GetProperty("level").GetString().ShouldBe("link");
        result.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe([TestUser.Alex.Email]);
    }

    [Test]
    public async Task Share_WhenNotTheOwner_ShouldChangeNothing()
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Mira.Email);
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        using var put = await alex.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "link", viewers = new[] { TestUser.Alex.Email } });
        using var namedPerson = await mira.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "link", viewers = new[] { TestUser.Mira.Email } });
        using var get = await alex.GetAsync($"/api/templates/{id}/sharing");
        using var unknown = await alex.PutAsJsonAsync("/api/templates/tpl_nosuchthing1/sharing", new { level = "link", viewers = Array.Empty<string>() });

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        namedPerson.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AccessAsync(id)).ShouldBe(DeckSharing.Named);
        (await ViewersAsync(id)).ShouldBe([TestUser.Mira.Email]);
    }

    [Test]
    public async Task Share_WhenOverTheViewerCap_ShouldChangeNothing()
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);

        using var response = await Client.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "link", viewers = Emails(ViewerEmails.MAX_VIEWERS + 1) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(ViewerEmails.TOO_MANY);
        (await AccessAsync(id)).ShouldBe(DeckSharing.Named);
        (await ViewersAsync(id)).ShouldBe([TestUser.Alex.Email]);
    }

    [Test]
    public async Task Share_WhenExactlyAtTheViewerCap_ShouldStoreThem()
    {
        var id = await PublishedAsync();

        using var response = await Client.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "named", viewers = Emails(ViewerEmails.MAX_VIEWERS) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ViewersAsync(id)).Count.ShouldBe(ViewerEmails.MAX_VIEWERS);
    }

    [TestCase("not-an-address")]
    [TestCase("")]
    [TestCase("Alex <alex@team.example>")]
    public async Task Share_WhenAnEmailIsNotAnAddress_ShouldChangeNothing(string email)
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);

        using var response = await Client.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "link", viewers = new[] { TestUser.Mira.Email, email } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AccessAsync(id)).ShouldBe(DeckSharing.Named);
        (await ViewersAsync(id)).ShouldBe([TestUser.Alex.Email]);
    }

    [Test]
    public async Task Share_WhenAnUnpublishedDraft_ShouldStoreTheLevelAndKeepItOffThePublicPage()
    {
        var id = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, await ManifestAsync());
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var put = await Client.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "link", viewers = Array.Empty<string>() });
        using var page = await anonymous.GetAsync($"/api/templates/{id}/public");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Share_WhenSetBackToPrivate_ShouldAnswerNotFoundToThem()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);
        using var alex = ClientFor(TestUser.Alex);
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var open = await alex.GetAsync($"/api/templates/{id}/public");

        using var back = await Client.PutAsJsonAsync($"/api/templates/{id}/sharing", new { level = "private", viewers = new[] { TestUser.Alex.Email } });
        using var asAlex = await alex.GetAsync($"/api/templates/{id}/public");
        using var signedOut = await anonymous.GetAsync($"/api/templates/{id}/public");
        using var unknown = await alex.GetAsync("/api/templates/tpl_nosuchthing1/public");
        using var asOwner = await Client.GetAsync($"/api/templates/{id}/public");

        open.StatusCode.ShouldBe(HttpStatusCode.OK);
        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        signedOut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await asAlex.Content.ReadAsStringAsync()).ShouldBe((await unknown.Content.ReadAsStringAsync()).Replace("tpl_nosuchthing1", id));
        asOwner.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListedForAsync(TestUser.Alex)).ShouldNotContain(id);
    }

    [Test]
    public async Task Public_WhenAccessIsLink_ShouldAnswerSignedOut()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.GetAsync($"/api/templates/{id}/public");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        page.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "name", "owner", "purpose", "look"]);
        page.GetProperty("id").GetString().ShouldBe(id);
        page.GetProperty("owner").GetString().ShouldBe(TestUser.Sam.Name);
        page.GetProperty("look").GetProperty("template").GetProperty("name").GetString().ShouldBe(id);
    }

    [Test]
    public async Task Public_WhenAccessIsNamed_ShouldAnswerTheNamedPeopleOnly()
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, "Alex@Team.Example");
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var named = await alex.GetAsync($"/api/templates/{id}/public");
        using var other = await mira.GetAsync($"/api/templates/{id}/public");
        using var signedOut = await anonymous.GetAsync($"/api/templates/{id}/public");

        named.StatusCode.ShouldBe(HttpStatusCode.OK);
        other.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        signedOut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Public_WhenAccessIsPrivate_ShouldBeNotFound()
    {
        var id = await PublishedAsync();
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);
        using var alex = ClientFor(TestUser.Alex);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var asAlex = await alex.GetAsync($"/api/templates/{id}/public");
        using var signedOut = await anonymous.GetAsync($"/api/templates/{id}/public");
        using var unknown = await anonymous.GetAsync("/api/templates/tpl_nosuchthing1/public");
        using var asOwner = await Client.GetAsync($"/api/templates/{id}/public");

        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        signedOut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await signedOut.Content.ReadAsStringAsync()).ShouldBe((await unknown.Content.ReadAsStringAsync()).Replace("tpl_nosuchthing1", id));
        asOwner.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Public_WhenAnArchivedTemplateIsShared_ShouldBeNotFoundToOthers()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var archive = await Client.PostAsync($"/api/templates/{id}/archive", null);
        using var alex = ClientFor(TestUser.Alex);

        using var asAlex = await alex.GetAsync($"/api/templates/{id}/public");

        archive.IsSuccessStatusCode.ShouldBeTrue();
        asAlex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Public_WhenASignedInPersonOpensTheLink_ShouldWriteNothing()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var alex = ClientFor(TestUser.Alex);

        using var response = await alex.GetAsync($"/api/templates/{id}/public");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ViewersAsync(id)).ShouldBeEmpty();
        (await ListedForAsync(TestUser.Alex)).ShouldNotContain(id);
    }

    [Test]
    public async Task Use_WhenAccessIsLink_ShouldRememberThemOnce()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var alex = ClientFor(TestUser.Alex);

        using var first = await alex.PostAsync($"/api/templates/{id}/use", null);
        using var second = await alex.PostAsync($"/api/templates/{id}/use", null);
        using var owner = await Client.PostAsync($"/api/templates/{id}/use", null);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        owner.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ViewersAsync(id)).ShouldBe([TestUser.Alex.Email]);
        (await ListedForAsync(TestUser.Alex)).ShouldContain(id);
    }

    [Test]
    public async Task Use_WhenTheRowIsMixedCase_ShouldNotAddAnother()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        await TemplateRowSeed.ViewerAsync(Factory, id, "Alex@Team.Example");
        using var alex = ClientFor(TestUser.Alex);

        using var response = await alex.PostAsync($"/api/templates/{id}/use", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ViewersAsync(id)).ShouldBe(["Alex@Team.Example"]);
    }

    [Test]
    public async Task Use_WhenBackToNamedPeople_ShouldKeepTheRowAndStopForOthers()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);
        (await alex.PostAsync($"/api/templates/{id}/use", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var back = await agent.CallToolAsync("set_sharing", new { template = id, level = "named" });
        using var miraUse = await mira.PostAsync($"/api/templates/{id}/use", null);

        back.IsError.ShouldBeFalse();
        miraUse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ViewersAsync(id)).ShouldBe([TestUser.Alex.Email]);
        (await ListedForAsync(TestUser.Alex)).ShouldContain(id);
    }

    [Test]
    public async Task Use_WhenAccessIsPrivate_ShouldBeNotFound()
    {
        var id = await PublishedAsync();
        using var alex = ClientFor(TestUser.Alex);

        using var response = await alex.PostAsync($"/api/templates/{id}/use", null);
        using var unknown = await alex.PostAsync("/api/templates/tpl_nosuchthing1/use", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe((await unknown.Content.ReadAsStringAsync()).Replace("tpl_nosuchthing1", id));
        (await ViewersAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Use_WhenSignedOut_ShouldBe401()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var anonymous = ClientFor(TestUser.Anonymous);

        using var response = await anonymous.PostAsync($"/api/templates/{id}/use", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ViewersAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Use_WhenNamedAndNotListed_ShouldBeNotFound()
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Mira.Email);
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        using var notListed = await alex.PostAsync($"/api/templates/{id}/use", null);
        using var listed = await mira.PostAsync($"/api/templates/{id}/use", null);

        notListed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        listed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ViewersAsync(id)).ShouldBe([TestUser.Mira.Email]);
    }

    [Test]
    public async Task SetSharing_WhenTemplateOnTheWire_ShouldReturnAnObject()
    {
        var id = await PublishedAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"template":"{{id}}","level":"named","viewers":["Alex@Team.Example"]}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.GetProperty("level").GetString().ShouldBe("named");
        structured.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(["alex@team.example"]);
        structured.GetProperty("link").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/t/{id}");
        (await AccessAsync(id)).ShouldBe(DeckSharing.Named);
    }

    [Test]
    public async Task SetSharing_WhenBothDeckAndTemplate_ShouldRefuse()
    {
        var id = await PublishedAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var both = await agent.CallToolAsync("set_sharing", new { deck, template = id, level = "link" });
        var neither = await agent.CallToolAsync("set_sharing", new { level = "link" });

        both.IsError.ShouldBeTrue();
        both.Json.GetString().ShouldBe("give deck or template, not both");
        neither.IsError.ShouldBeTrue();
        neither.Json.GetString().ShouldBe("deck: required");
        (await AccessAsync(id)).ShouldBe(DeckSharing.Private);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("private");
    }

    [Test]
    public async Task SetSharing_WhenTemplateIsNotTheirs_ShouldAnswerAsForAnUnknownOne()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);

        var theirs = await alex.CallToolAsync("set_sharing", new { template = id, level = "private" });
        var unknown = await alex.CallToolAsync("set_sharing", new { template = "tpl_nosuchthing1", level = "private" });
        var badLevel = await alex.CallToolAsync("set_sharing", new { template = id, level = "public" });

        theirs.IsError.ShouldBeTrue();
        theirs.Json.GetString().ShouldBe(unknown.Json.GetString()!.Replace("tpl_nosuchthing1", id));
        badLevel.IsError.ShouldBeTrue();
        badLevel.Json.GetString().ShouldBe("level: must be \"private\", \"link\" or \"named\"");
        (await AccessAsync(id)).ShouldBe(DeckSharing.Link);
    }

    [Test]
    public async Task ListFor_WhenTheViewerRowIsMixedCase_ShouldIncludeIt()
    {
        var id = await PublishedAsync(DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, "Alex@TEAM.example");

        var listed = await ListedForAsync(TestUser.Alex);

        listed.ShouldContain(id);
    }

    // The same 400 as an unknown id, and nothing written, for a template the caller may not pick.
    [Test]
    public async Task Deck_WhenPickingATemplateTheCallerMayNotPick_ShouldAnswerAsForAnUnknownOne()
    {
        var privateOne = await PublishedAsync();
        var linkNoRow = await PublishedAsync(DeckSharing.Link);
        var namedNotListed = await PublishedAsync(DeckSharing.Named);
        using var alex = ClientFor(TestUser.Alex);
        var own = await SharingApi.CreateDeckAsync(alex, "Alex's deck");
        var before = await DeckCountAsync(TestUser.Alex);
        var ownTemplate = await DeckTemplateAsync(own);

        foreach (var template in new[] { privateOne, linkNoRow, namedNotListed })
        {
            using var create = await alex.PostAsJsonAsync("/api/decks", new { title = "Not allowed", template });
            using var update = await alex.PutAsJsonAsync($"/api/decks/{own}/look", new { template });

            create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await create.Content.ReadAsStringAsync()).ShouldBe($$"""{"error":"{{LookRules.UnknownTemplate(template)}}"}""");
            (await update.Content.ReadAsStringAsync()).ShouldBe($$"""{"error":"{{LookRules.UnknownTemplate(template)}}"}""");
        }
        using var unknownCreate = await alex.PostAsJsonAsync("/api/decks", new { title = "Not allowed", template = "tpl_nosuchthing1" });

        (await unknownCreate.Content.ReadAsStringAsync()).ShouldBe($$"""{"error":"{{LookRules.UnknownTemplate("tpl_nosuchthing1")}}"}""");
        (await DeckCountAsync(TestUser.Alex)).ShouldBe(before);
        (await DeckTemplateAsync(own)).ShouldBe(ownTemplate);
    }

    [Test]
    public async Task Deck_WhenTheCallerUsedTheLink_ShouldMayPickTheTemplate()
    {
        var id = await PublishedAsync(DeckSharing.Link);
        using var alex = ClientFor(TestUser.Alex);
        using var before = await alex.PostAsJsonAsync("/api/decks", new { title = "Early", template = id });
        (await alex.PostAsync($"/api/templates/{id}/use", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var after = await alex.PostAsJsonAsync("/api/decks", new { title = "Late", template = id });

        before.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        after.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static string[] Emails(int count) => [.. Enumerable.Range(0, count).Select(n => $"p{n}@team.example")];

    // One valid manifest from the editor, made once: a person may own at most 50 templates.
    private async Task<string> ManifestAsync()
    {
        if (_manifest != null)
        {
            return _manifest;
        }
        var created = await Client.PostAsJsonAsync("/api/templates", new { basedOn = "talk", name = "Mine" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var editor = await Client.GetFromJsonAsync<JsonElement>($"/api/templates/{id}");
        _manifest = JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!.ToJsonString();
        return _manifest;
    }

    private async Task<string> PublishedAsync(DeckSharing access = DeckSharing.Private)
        => await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, await ManifestAsync(), access: access);

    private async Task<DeckSharing> AccessAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Templates.AsNoTracking().Where(t => t.Id == id).Select(t => t.Access).SingleAsync();
    }

    private async Task<List<string>> ViewersAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateViewers.AsNoTracking().Where(v => v.TemplateId == id).Select(v => v.Email).ToListAsync();
    }

    private async Task<int> DeckCountAsync(TestUser person)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.CountAsync(d => d.OwnerId == user.Id);
    }

    private async Task<string> DeckTemplateAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.AsNoTracking().Where(d => d.Id == deck).Select(d => d.Template).SingleAsync();
    }

    // The refs of the templates the person may pick, through the host's own ITemplates.
    private async Task<IReadOnlyList<string>> ListedForAsync(TestUser person)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        var listed = await scope.ServiceProvider.GetRequiredService<ITemplates>().ListForAsync(new Actor(user.Id, user.Email), CancellationToken.None);
        return [.. listed.Select(t => t.Ref)];
    }

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
