using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Rendering;

// The card cache (L9): ICardRenderer keys a deck's card by RenderKey and keeps it under renders/<deck>/ on the data
// volume, so only a new revision or look renders again. Only a link deck's card is rendered; any other deck gets the
// generic card. The generic card renders once per process, one key
// renders once however many ask at once, and a failure is never kept. FakeRenderEngine counts the renders.
[TestFixture]
public class CardCacheTests : ApiTestFixture
{
    private McpTestClient _agent = null!;

    private FakeRenderEngine Engine => Factory.Services.GetRequiredService<FakeRenderEngine>();

    private ICardRenderer Cards => Factory.Services.GetRequiredService<ICardRenderer>();

    [OneTimeSetUp]
    public async Task ConnectAgent() => _agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

    [OneTimeTearDown]
    public async Task DisconnectAgent() => await _agent.DisposeAsync();

    [TearDown]
    public void StopThrowing() => Engine.ThrowOnRender = null;

    [Test]
    public async Task Card_CacheHitDoesNotCallTheEngine()
    {
        var deck = await LinkDeckAsync();

        var first = await Cards.RenderCardAsync(deck, CancellationToken.None);
        var second = await Cards.RenderCardAsync(deck, CancellationToken.None);

        RendersOf(deck).ShouldBe(1);
        second.Key.ShouldBe(first.Key);
        second.Png.ShouldBe(FakeRenderEngine.PNG);
        CardFiles(deck).ShouldHaveSingleItem().ShouldEndWith($"card-{string.Join('-', first.Key.Split(':').Skip(1))}.png");
    }

    [TestCase("revision")]
    [TestCase("look")]
    public async Task Card_NewRevisionLookOrSharingRendersAgain(string change)
    {
        var deck = await LinkDeckAsync();
        var before = await Cards.RenderCardAsync(deck, CancellationToken.None);

        await ChangeAsync(deck, change);
        var after = await Cards.RenderCardAsync(deck, CancellationToken.None);

        RendersOf(deck).ShouldBe(2);
        after.Key.ShouldNotBe(before.Key);
        CardFiles(deck).ShouldHaveSingleItem("the older card is deleted when the new one is written");
    }

    [Test]
    public async Task Card_GenericCardRendersOncePerProcess()
    {
        var cards = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Cards.RenderGenericCardAsync(CancellationToken.None)));
        var later = await Cards.RenderGenericCardAsync(CancellationToken.None);

        Engine.Calls.Count(call => call == FakeRenderEngine.GENERIC).ShouldBe(1);
        cards.ShouldAllBe(card => card.Key == RenderKey.GENERIC);
        later.Png.ShouldBe(FakeRenderEngine.PNG);
    }

    [Test]
    public async Task Card_ConcurrentRequestsRenderOnce()
    {
        var deck = await LinkDeckAsync();

        var cards = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Cards.RenderCardAsync(deck, CancellationToken.None)));

        RendersOf(deck).ShouldBe(1);
        cards.Select(card => card.Key).Distinct().ShouldHaveSingleItem();
    }

    [Test]
    public async Task Card_FailureIsNotCached()
    {
        var deck = await LinkDeckAsync();
        Engine.ThrowTimeout();

        await Should.ThrowAsync<TimeoutException>(() => Cards.RenderCardAsync(deck, CancellationToken.None));
        CardFiles(deck).ShouldBeEmpty();
        Engine.ThrowOnRender = null;
        var card = await Cards.RenderCardAsync(deck, CancellationToken.None);

        RendersOf(deck).ShouldBe(2);
        card.Png.ShouldBe(FakeRenderEngine.PNG);
    }

    // Only a link deck's card is ever rendered (L9, D12), so an unknown deck, a private one, a named-viewer one and a
    // deck no longer shared by link all get the generic card and never start a deck render.
    [Test]
    public async Task Card_UnknownDeckNeverStartsARender()
    {
        var card = await Cards.RenderCardAsync("dk_MISSING1", CancellationToken.None);

        card.Key.ShouldBe(RenderKey.GENERIC);
        RendersOf("dk_MISSING1").ShouldBe(0);
    }

    [TestCase("private")]
    [TestCase("named")]
    public async Task Card_PrivateDeckGetsTheGenericCard(string sharing)
    {
        var deck = await DeckSeed.CreateDeckAsync(_agent);
        if (sharing == "named")
        {
            await SharingApi.PutOkAsync(Client, deck, "named", "alex@team.example");
        }

        var card = await Cards.RenderCardAsync(deck, CancellationToken.None);

        card.Key.ShouldBe(RenderKey.GENERIC);
        card.Png.ShouldBe(FakeRenderEngine.PNG);
        Engine.Calls.ShouldContain(FakeRenderEngine.GENERIC);
        RendersOf(deck).ShouldBe(0);
        CardFiles(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Card_DeckNoLongerSharedByLinkGetsTheGenericCard()
    {
        var deck = await LinkDeckAsync();
        await Cards.RenderCardAsync(deck, CancellationToken.None);

        await SharingApi.PutOkAsync(Client, deck, "private");
        var card = await Cards.RenderCardAsync(deck, CancellationToken.None);

        card.Key.ShouldBe(RenderKey.GENERIC);
        RendersOf(deck).ShouldBe(1);
    }

    private async Task ChangeAsync(string deck, string change)
    {
        switch (change)
        {
            case "revision":
                await DeckSeed.AddQuoteAsync(_agent, deck, "Ship it on Tuesday.");
                break;
            default:
                using (var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { palette = "ember" }))
                {
                    response.EnsureSuccessStatusCode();
                }
                break;
        }
    }

    private async Task<string> LinkDeckAsync()
    {
        var deck = await DeckSeed.CreateDeckAsync(_agent);
        await SharingApi.PutOkAsync(Client, deck, "link");
        return deck;
    }

    private int RendersOf(string deck) => Engine.Calls.Count(call => call == deck);

    private string[] CardFiles(string deck)
    {
        var folder = Path.Combine(Factory.Services.GetRequiredService<IConfiguration>()[ImageStore.DATA_PATH_SETTING]!, "renders", deck);
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }
}
