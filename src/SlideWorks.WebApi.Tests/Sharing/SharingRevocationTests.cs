using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// Taking access away works on the next request: the player, the deck's images and a new JoinDeck all refuse the person
// as if the deck didn't exist. Images send no-cache with an ETag, so a browser revalidates; a revoked person's
// conditional request must get 404, never a 304 that would let them keep showing a cached copy.
[TestFixture]
[CancelAfter(30000)]
public class SharingRevocationTests : ApiTestFixture
{
    [Test]
    public async Task Put_WhenOneViewerRemovedAndDeckStaysNamed_ShouldRefuseOnlyThem()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email, TestUser.Mira.Email);
        (await alex.GetAsync($"/api/play/{deck}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var view = await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Mira.Email);
        var alexPlay = await alex.GetAsync($"/api/play/{deck}");
        var miraPlay = await mira.GetAsync($"/api/play/{deck}");
        await using var alexHub = await HubReceiver.StartAsync(Factory, TestUser.Alex);
        await using var miraHub = await HubReceiver.StartAsync(Factory, TestUser.Mira);
        var refusal = await Should.ThrowAsync<HubException>(() => alexHub.Connection.InvokeAsync("JoinDeck", deck));

        view.GetProperty("level").GetString().ShouldBe("named");
        view.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe([TestUser.Mira.Email]);
        alexPlay.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await alexPlay.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        refusal.Message.ShouldContain(DeckHub.DECK_NOT_FOUND, Case.Sensitive);
        miraPlay.StatusCode.ShouldBe(HttpStatusCode.OK);
        await Should.NotThrowAsync(() => miraHub.Connection.InvokeAsync("JoinDeck", deck));
    }

    [Test]
    public async Task ImageRevalidation_WhenViewerRemoved_ShouldReturn404Not304()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email, TestUser.Mira.Email);
        using var cached = await alex.GetAsync($"/api/images/{image}");
        cached.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var stillFresh = await RevalidateAsync(alex, image, cached))
        {
            stillFresh.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Mira.Email);
        using var alexAgain = await RevalidateAsync(alex, image, cached);
        using var miraAgain = await RevalidateAsync(mira, image, cached);

        alexAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await alexAgain.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Image not found");
        miraAgain.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Test]
    public async Task ImageRevalidation_WhenLinkDeckMadePrivate_ShouldReturn404Not304ToAnonymous()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        await SharingApi.PutOkAsync(Client, deck, "link");
        using var cached = await anonymous.GetAsync($"/api/images/{image}");
        cached.StatusCode.ShouldBe(HttpStatusCode.OK);

        await SharingApi.PutOkAsync(Client, deck, "private");
        using var again = await RevalidateAsync(anonymous, image, cached);

        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await again.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Image not found");
    }

    private static async Task<HttpResponseMessage> RevalidateAsync(HttpClient client, string image, HttpResponseMessage cached)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{image}");
        request.Headers.IfNoneMatch.Add(cached.Headers.ETag!);
        return await client.SendAsync(request);
    }
}
