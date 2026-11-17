using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Images;

// L6: POST /api/decks/{id}/images is the Look panel's upload, the same path as upload_image. Owner only; the bytes then go
// to PUT /api/uploads/{token}, and the image can be the deck's logo.
[TestFixture]
public class BrowserUploadTests : ApiTestFixture
{
    [Test]
    public async Task Post_WhenOwner_ShouldReturnTheUploadAndCompleteThroughTheUploadUrl()
    {
        var deck = await CreateDeckAsync();

        var response = await Client.PostAsJsonAsync($"/api/decks/{deck}/images", new { name = "logo.png", contentType = "image/png" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var started = await response.Content.ReadFromJsonAsync<JsonElement>();
        Should.NotThrow(() => JsonShape.AssertMatches(started, ExampleResponse(), "image-upload.json response"));
        started.GetProperty("method").GetString().ShouldBe("PUT");
        var image = started.GetProperty("image").GetString()!;

        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, ImageSeed.SamplePng());
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());

        using var served = await Client.GetAsync($"/api/images/{image}");
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        var logo = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { logo = image });
        logo.StatusCode.ShouldBe(HttpStatusCode.OK, await logo.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Post_ShouldRecordTheStartFromTheWeb()
    {
        var deck = await CreateDeckAsync();

        (await Client.PostAsJsonAsync($"/api/decks/{deck}/images", new { name = "logo.png", contentType = "image/png" })).StatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("started uploading logo.png");
        entry.GetProperty("via").GetString().ShouldBe("web");
        entry.GetProperty("tool").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Post_WhenNotTheOwner_ShouldReturn404()
    {
        var deck = await CreateDeckAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.PostAsJsonAsync($"/api/decks/{deck}/images", new { name = "logo.png", contentType = "image/png" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Deck not found");
    }

    [Test]
    public async Task Post_WhenSignedOut_ShouldReturn401()
    {
        var deck = await CreateDeckAsync();
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsJsonAsync($"/api/decks/{deck}/images", new { name = "logo.png", contentType = "image/png" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase("""{ "contentType": "image/png" }""", "name: required")]
    [TestCase("""{ "name": "logo.svg", "contentType": "image/svg+xml" }""", "contentType: must be image/png, image/jpeg, image/webp or image/gif")]
    public async Task Post_WhenInvalid_ShouldGiveTheToolsMessage(string body, string error)
    {
        var deck = await CreateDeckAsync();

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PostAsync($"/api/decks/{deck}/images", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(error);
    }

    private async Task<string> CreateDeckAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Logo" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static JsonElement ExampleResponse()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "image-upload.json")));
        return contract.RootElement.GetProperty("response").Clone();
    }
}
