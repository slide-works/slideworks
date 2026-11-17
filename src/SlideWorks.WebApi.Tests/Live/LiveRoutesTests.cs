using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Live;

// J2: POST, GET and DELETE /api/decks/{id}/live, for the deck's owner only.
[TestFixture]
public class LiveRoutesTests : ApiTestFixture
{
    private readonly OneShotSaveGate _gate = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_gate));
        FakeAudienceRelay.AddTo(services);
        services.AddSingleton<PresentationStartSpy>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<PresentationStartSpy>());
    };

    private FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    private PresentationStartSpy Presentations => Factory.Services.GetRequiredService<PresentationStartSpy>();

    [Test]
    public async Task LivePost_WhenNotOwner_ShouldRefuseWithNoSessionCreated()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared");
        await SharingApi.PutOkAsync(Client, shared, "link");
        var hidden = await SharingApi.CreateDeckAsync(Client, "Hidden");
        using var alex = ClientFor(TestUser.Alex);

        var forbidden = await alex.PostAsync($"/api/decks/{shared}/live", null);
        var notFound = await alex.PostAsync($"/api/decks/{hidden}/live", null);
        var endForbidden = await alex.DeleteAsync($"/api/decks/{shared}/live");

        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        endForbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetLiveAsync(shared)).GetProperty("live").GetBoolean().ShouldBeFalse();
        (await GetLiveAsync(hidden)).GetProperty("live").GetBoolean().ShouldBeFalse();
        Relay.Started.ShouldNotContain(shared);
        Relay.Started.ShouldNotContain(hidden);
        Presentations.Started.ShouldNotContain(shared);
        Presentations.Started.ShouldNotContain(hidden);
        (await DeckSeed.ActivityAsync(Client, shared)).ShouldNotContain(e => e.GetProperty("summary").GetString() == "went live");
    }

    [Test]
    public async Task LivePost_WhenOwner_ShouldOpenSessionAndStartThePresentation()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var live = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(live, "live.json", ScreenShapes.Live);
        live.GetProperty("live").GetBoolean().ShouldBeTrue();
        live.GetProperty("sessionId").GetString().ShouldNotBeNullOrEmpty();
        live.GetProperty("code").GetString()!.Length.ShouldBe(6);
        live.GetProperty("codeLabel").GetString().ShouldBe($"{live.GetProperty("code").GetString()![..3]} {live.GetProperty("code").GetString()![3..]}");
        live.GetProperty("joinUrl").GetString().ShouldBe($"http://localhost/j/{live.GetProperty("code").GetString()}");
        live.GetProperty("hostLabel").GetString().ShouldBe("localhost/j");
        live.GetProperty("joinBar").GetBoolean().ShouldBeFalse();
        Presentations.Started.ShouldContain(deck);
        Relay.Started.Where(id => id == deck).ShouldHaveSingleItem();
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("went live");
    }

    [Test]
    public async Task LivePost_WhenAlreadyLive_ShouldReturnSameSession()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var first = await PostLiveAsync(deck);
        var second = await PostLiveAsync(deck);

        second.GetProperty("sessionId").GetString().ShouldBe(first.GetProperty("sessionId").GetString());
        Relay.Started.Count(id => id == deck).ShouldBe(1);
        (await GetLiveAsync(deck)).GetProperty("sessionId").GetString().ShouldBe(first.GetProperty("sessionId").GetString());
    }

    [Test]
    public async Task LivePost_WhenCallsRace_ShouldReturnOneSession()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostLiveAsync(deck)));

        results.Select(r => r.GetProperty("sessionId").GetString()).Distinct().Count().ShouldBe(1);
        Relay.Started.Count(id => id == deck).ShouldBe(1);
    }

    [Test]
    public async Task LivePost_WhenAnotherCallOpensTheSessionFirst_ShouldReturnThatSession()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        JsonElement? winner = null;
        _gate.Before = async () => winner = await PostLiveAsync(deck);

        var loser = await PostLiveAsync(deck);

        winner.ShouldNotBeNull();
        loser.GetProperty("sessionId").GetString().ShouldBe(winner.Value.GetProperty("sessionId").GetString());
        Relay.Started.Where(id => id == deck).ShouldHaveSingleItem();
        (await DeckSeed.ActivityAsync(Client, deck)).Count(e => e.GetProperty("summary").GetString() == "went live").ShouldBe(1);
    }

    [Test]
    public async Task LiveDelete_WhenNotOwnerAndDeckIsLive_ShouldRefuseAndStayLive()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck, "link");
        await PostLiveAsync(deck);
        using var alex = ClientFor(TestUser.Alex);

        var refused = await alex.DeleteAsync($"/api/decks/{deck}/live");

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetLiveAsync(deck)).GetProperty("live").GetBoolean().ShouldBeTrue();
        Relay.Ended.ShouldNotContain(deck);
        (await DeckSeed.ActivityAsync(Client, deck)).ShouldNotContain(e => e.GetProperty("summary").GetString() == "ended live session");
    }

    [Test]
    public async Task LiveGet_WhenNotOwner_ShouldRefuseByVisibility()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared");
        await SharingApi.PutOkAsync(Client, shared, "link");
        var hidden = await SharingApi.CreateDeckAsync(Client, "Hidden");
        using var alex = ClientFor(TestUser.Alex);

        var forbidden = await alex.GetAsync($"/api/decks/{shared}/live");
        var notFound = await alex.GetAsync($"/api/decks/{hidden}/live");
        var hiddenDelete = await alex.DeleteAsync($"/api/decks/{hidden}/live");

        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        hiddenDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task LiveGet_WhenAnonymousOnLinkDeck_ShouldBeForbiddenNotChallenged()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared");
        await SharingApi.PutOkAsync(Client, shared, "link");
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/decks/{shared}/live");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Only the deck's owner can go live");
    }

    [Test]
    public async Task LiveGet_WhenAnonymousOnPrivateDeck_ShouldBeNotFound()
    {
        var hidden = await SharingApi.CreateDeckAsync(Client, "Hidden");
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/decks/{hidden}/live");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task LiveGet_WhenPendingPersonOnLinkDeck_ShouldBeForbiddenButNotPendingApproval()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared");
        await SharingApi.PutOkAsync(Client, shared, "link");
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync($"/api/decks/{shared}/live");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, body);
        body.ShouldNotContain("pending_approval");
    }

    [Test]
    public async Task LiveGet_WhenAnonymousAndDeckIsLive_ShouldNeverReturnALiveView()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared");
        await SharingApi.PutOkAsync(Client, shared, "link");
        await PostLiveAsync(shared);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/decks/{shared}/live");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("sessionId");
    }

    [Test]
    public async Task LivePostAndDelete_WhenAnonymous_ShouldStayBehindTheGate()
    {
        var shared = await SharingApi.CreateDeckAsync(Client, "Shared");
        await SharingApi.PutOkAsync(Client, shared, "link");
        using var anonymous = ClientFor(TestUser.Anonymous);

        var post = await anonymous.PostAsync($"/api/decks/{shared}/live", null);
        var delete = await anonymous.DeleteAsync($"/api/decks/{shared}/live");

        post.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await GetLiveAsync(shared)).GetProperty("live").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task LiveGet_WhenOwner_ShouldReturnTheLiveView()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await Client.GetAsync($"/api/decks/{deck}/live");
        var live = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonShape.AssertMatches(live, "live.json", ScreenShapes.Live);
    }

    [Test]
    public async Task LookPut_WhenOnlyJoinBarChanges_ShouldKeepTheOtherChromeOverrides()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        (await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { chrome = new { tracker = false, footer = "none" } })).EnsureSuccessStatusCode();

        var toggled = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { chrome = new { joinBar = true } });
        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        toggled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var overrides = play.GetProperty("look").GetProperty("chromeOverrides");
        overrides.GetProperty("joinBar").GetBoolean().ShouldBeTrue();
        overrides.GetProperty("tracker").GetBoolean().ShouldBeFalse();
        overrides.GetProperty("footer").GetString().ShouldBe("none");
    }

    [Test]
    public async Task Render_WhenLive_ShouldCarryTheJoinObject()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var live = await PostLiveAsync(deck);
        var token = Factory.Services.GetRequiredService<IRenderTokens>().Issue(deck, RenderPurpose.Preview);
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/render/{deck}");
        request.Headers.Add(RenderController.TOKEN_HEADER, token);

        using var response = await anonymous.SendAsync(request);
        var render = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var join = render.GetProperty("join");
        join.GetProperty("code").GetString().ShouldBe(live.GetProperty("code").GetString());
        join.GetProperty("joinUrl").GetString().ShouldBe(live.GetProperty("joinUrl").GetString());
        join.GetProperty("live").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task LiveDelete_WhenLive_ShouldEndAndPublishLiveChanged()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var changes = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => changes.Writer.WriteAsync(e).AsTask());
        await PostLiveAsync(deck);

        var response = await Client.DeleteAsync($"/api/decks/{deck}/live");
        var again = await Client.DeleteAsync($"/api/decks/{deck}/live");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetLiveAsync(deck)).GetProperty("live").GetBoolean().ShouldBeFalse();
        Relay.Ended.Where(id => id == deck).ShouldHaveSingleItem();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        (await changes.Reader.ReadAsync(timeout.Token)).ShouldBeOfType<LiveChanged>();
        (await changes.Reader.ReadAsync(timeout.Token)).ShouldBeOfType<LiveChanged>();
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("ended live session");
    }

    [Test]
    public async Task LiveGet_WhenNeverLive_ShouldReturnLiveFalse()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var live = await GetLiveAsync(deck);

        JsonShape.AssertMatches(live, "live.json", ScreenShapes.Live);
        live.GetProperty("live").GetBoolean().ShouldBeFalse();
        live.GetProperty("sessionId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Play_WhenLive_ShouldCarryTheJoinObjectWithLiveTrue()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck, "link");
        var live = await PostLiveAsync(deck);
        using var anonymous = ClientFor(TestUser.Anonymous);

        var play = await anonymous.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        foreach (var join in new[] { play.GetProperty("join"), page.GetProperty("join") })
        {
            join.GetProperty("code").GetString().ShouldBe(live.GetProperty("code").GetString());
            join.GetProperty("joinUrl").GetString().ShouldBe(live.GetProperty("joinUrl").GetString());
            join.GetProperty("hostLabel").GetString().ShouldBe("localhost/j");
            join.GetProperty("live").GetBoolean().ShouldBeTrue();
        }
    }

    private async Task<JsonElement> PostLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<JsonElement> GetLiveAsync(string deck)
    {
        var response = await Client.GetAsync($"/api/decks/{deck}/live");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
