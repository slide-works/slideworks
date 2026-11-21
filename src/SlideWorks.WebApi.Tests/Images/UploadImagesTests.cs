using System.Net;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Images;

// upload_images: one call, one PUT URL per file, all or nothing. A refusal leaves the deck's change log as it was (the
// deck's creation is its only entry), so no upload row was written either.
[TestFixture]
[CancelAfter(30000)]
public class UploadImagesTests : ApiTestFixture
{
    [Test]
    public async Task UploadImages_WhenTwoFilesAreNamed_ShouldReturnTwoPutUrlsThatComplete()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var png = ImageSeed.Bytes("image/png");
        var jpeg = ImageSeed.Bytes("image/jpeg");

        var result = await SlideCalls.OkAsync(agent, "upload_images", new
        {
            deck,
            files = new[] { new { name = "map.png", contentType = "image/png" }, new { name = "photo.jpg", contentType = "image/jpeg" } },
        });
        var images = result.GetProperty("images").EnumerateArray().ToList();

        images.Count.ShouldBe(2);
        images.Select(i => i.GetProperty("name").GetString()).ShouldBe(["map.png", "photo.jpg"]);
        foreach (var image in images)
        {
            image.EnumerateObject().Select(p => p.Name).ShouldBe(["image", "name", "uploadUrl", "method", "expiresInSeconds"], ignoreOrder: true);
            image.GetProperty("image").GetString()!.ShouldMatch("^img_[0-9A-Za-z]{10}$");
            image.GetProperty("uploadUrl").GetString()!.ShouldStartWith(TestAppFactory.PUBLIC_URL + "/api/uploads/", Case.Sensitive);
            image.GetProperty("method").GetString().ShouldBe("PUT");
            image.GetProperty("expiresInSeconds").GetInt32().ShouldBe(600);
        }
        images[0].GetProperty("uploadUrl").GetString().ShouldNotBe(images[1].GetProperty("uploadUrl").GetString());

        using var firstPut = await ImageSeed.PutAsync(Factory, images[0].GetProperty("uploadUrl").GetString()!, png);
        using var secondPut = await ImageSeed.PutAsync(Factory, images[1].GetProperty("uploadUrl").GetString()!, jpeg, "image/jpeg");
        firstPut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        secondPut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetByteArrayAsync($"/api/images/{images[0].GetProperty("image").GetString()}")).ShouldBe(png);
        (await Client.GetByteArrayAsync($"/api/images/{images[1].GetProperty("image").GetString()}")).ShouldBe(jpeg);

        var id = images[0].GetProperty("image").GetString()!;
        await DeckSeed.AddSlideAsync(agent, deck, "image", $$"""{ "headline": "The board", "image": { "image": "{{id}}", "alt": "A taskboard" } }""");
        (await SlideCalls.SlidesAsync(agent, deck))[0].GetProperty("slots").GetProperty("image").GetProperty("image").GetString().ShouldBe(id);
        var log = await DeckSeed.ActivityAsync(Client, deck);
        log.Count(e => e.GetProperty("tool").GetString() == "upload_images").ShouldBe(2);
    }

    [Test]
    public async Task UploadImages_WhenOneContentTypeIsRejected_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "upload_images", new
        {
            deck,
            files = new[] { new { name = "map.png", contentType = "image/png" }, new { name = "logo.svg", contentType = "image/svg+xml" } },
        });

        error.ShouldBe("files[1].contentType: must be image/png, image/jpeg, image/webp or image/gif");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task UploadImages_WhenNoFilesAreNamed_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "upload_images", new { deck, files = Array.Empty<object>() });

        error.ShouldBe("files: between 1 and 30 files");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task UploadImages_WhenThirtyOneFilesAreNamed_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var files = Enumerable.Range(0, 31).Select(i => new { name = $"{i}.png", contentType = "image/png" }).ToArray();

        var error = await SlideCalls.ErrorAsync(agent, "upload_images", new { deck, files });

        error.ShouldBe("files: between 1 and 30 files");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task UploadImages_WhenThirtyFilesAreNamed_ShouldReturnThirty()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var files = Enumerable.Range(0, 30).Select(i => new { name = $"{i}.png", contentType = "image/png" }).ToArray();

        var result = await SlideCalls.OkAsync(agent, "upload_images", new { deck, files });

        result.GetProperty("images").GetArrayLength().ShouldBe(30);
    }

    [Test]
    public async Task UploadImages_WhenTheDeckIsNotOwned_ShouldCreateNothing()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);

        var error = await SlideCalls.ErrorAsync(alex, "upload_images", new { deck, files = new[] { new { name = "a.png", contentType = "image/png" } } });

        error.ShouldBe("Deck not found");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }
}
