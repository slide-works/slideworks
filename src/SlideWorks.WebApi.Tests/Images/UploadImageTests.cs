using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Images;

// K11 and the anonymous routes of K1: upload_image hands out a one-time upload URL, an agent PUTs the bytes to it with no
// cookie and no Origin, and GET /api/images/{id} serves them to whoever may view the deck. An image whose upload never
// completed can't go on a slide.
[TestFixture]
[CancelAfter(30000)]
public class UploadImageTests : ApiTestFixture
{
    private const string UPLOAD_URL_PREFIX = TestAppFactory.PUBLIC_URL + "/api/uploads/";
    private const int TEN_MB = 10 * 1024 * 1024;

    [Test]
    public async Task UploadImage_ShouldReturnPutUrlAndImageId()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "upload_image", $$"""{"deck":"{{deck}}","name":"story-map.png","contentType":"image/png"}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
        var structured = result.GetProperty("structuredContent");
        structured.ValueKind.ShouldBe(JsonValueKind.Object);
        structured.EnumerateObject().Select(p => p.Name).ShouldBe(["image", "uploadUrl", "method", "expiresInSeconds"], ignoreOrder: true);
        structured.GetProperty("image").GetString()!.ShouldMatch("^img_[0-9A-Za-z]{10}$");
        var uploadUrl = structured.GetProperty("uploadUrl").GetString()!;
        uploadUrl.ShouldStartWith(UPLOAD_URL_PREFIX, Case.Sensitive);
        uploadUrl[UPLOAD_URL_PREFIX.Length..].ShouldMatch("^[A-Za-z0-9_-]{43}$");
        structured.GetProperty("method").GetString().ShouldBe("PUT");
        structured.GetProperty("expiresInSeconds").GetInt32().ShouldBe(600);
        JsonDocument.Parse(McpWire.TextOf(result)).RootElement.GetProperty("uploadUrl").GetString().ShouldBe(uploadUrl);
        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("started uploading story-map.png");
        entry.GetProperty("tool").GetString().ShouldBe("upload_image");
        entry.GetRawText().ShouldNotContain(uploadUrl[UPLOAD_URL_PREFIX.Length..], Case.Sensitive);
    }

    [Test]
    public async Task UploadImage_WhenNotOwner_ShouldSayDeckNotFound()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);

        var notOwner = await SlideCalls.ErrorAsync(alex, "upload_image", new { deck, name = "a.png", contentType = "image/png" });
        var unknown = await SlideCalls.ErrorAsync(sam, "upload_image", new { deck = "nOsUcHdEcK00", name = "a.png", contentType = "image/png" });

        notOwner.ShouldBe("Deck not found");
        unknown.ShouldBe("Deck not found");
        (await DeckSeed.ActivityAsync(Client, deck)).ShouldNotContain(e => e.GetProperty("tool").GetString() == "upload_image");
    }

    [TestCase("image/svg+xml")]
    [TestCase("text/html")]
    [TestCase("image/bmp")]
    public async Task UploadImage_WhenTypeNotAccepted_ShouldRefuse(string contentType)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "upload_image", new { deck, name = "map", contentType });

        error.ShouldBe("contentType: must be image/png, image/jpeg, image/webp or image/gif");
    }

    [Test]
    public async Task UploadImage_WhenNameBlank_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var error = await SlideCalls.ErrorAsync(agent, "upload_image", new { deck, name = "  ", contentType = "image/png" });

        error.ShouldBe("name: required");
    }

    // name is only a label, but a control character (a NUL above all) has no place in one and can break whatever shows it.
    // The cases are code points, because a test name holding a C1 character never matches a test filter, so it would
    // silently not run.
    [TestCase(0x00, "story{0}map.png")]
    [TestCase(0x0A, "story{0}map.png")]
    [TestCase(0x0D, "story-map.png{0}")]
    [TestCase(0x07, "{0}story-map.png")]
    [TestCase(0x7F, "story{0}map.png")]
    [TestCase(0x9F, "story{0}map.png")]
    public async Task UploadImage_WhenNameHasControlCharacter_ShouldRefuse(int codePoint, string shape)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var name = string.Format(System.Globalization.CultureInfo.InvariantCulture, shape, (char)codePoint);

        var error = await SlideCalls.ErrorAsync(agent, "upload_image", new { deck, name, contentType = "image/png" });

        error.ShouldBe("name: must not contain control characters");
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Put_WhenPng_ShouldStoreAndThenServeToOwner()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var image = started.GetProperty("image").GetString()!;
        var bytes = ImageSeed.SamplePng();

        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, bytes);
        using var get = await Client.GetAsync($"/api/images/{image}");

        put.StatusCode.ShouldBe(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        get.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        get.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        (await get.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        (await File.ReadAllBytesAsync(Path.Combine(DataPath(), "images", deck, image))).ShouldBe(bytes);
    }

    [TestCase("image/png")]
    [TestCase("image/jpeg")]
    [TestCase("image/webp")]
    [TestCase("image/gif")]
    public async Task Put_WhenEachAcceptedType_ShouldServeItWithThatType(string contentType)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var bytes = ImageSeed.Bytes(contentType);

        var image = await ImageSeed.UploadAsync(agent, Factory, deck, bytes, contentType, "picture");
        using var get = await Client.GetAsync($"/api/images/{image}");

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        get.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
        (await get.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    // The path comes from the server's ids only: a name that tries to climb out of the folder is just a label.
    [Test]
    public async Task Put_WhenNameClimbsFolders_ShouldStoreUnderServerIdsOnly()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var dataPath = DataPath();
        var before = Directory.Exists(dataPath) ? Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories).ToHashSet() : [];

        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"), name: "../../../escape.png");

        var added = Directory.GetFiles(Path.GetDirectoryName(dataPath)!, "escape.png", SearchOption.TopDirectoryOnly);
        added.ShouldBeEmpty();
        Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories).Except(before)
            .ShouldBe([Path.Combine(dataPath, "images", deck, image)]);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("started uploading ../../../escape.png");
    }

    [Test]
    public async Task Put_WhenTokenReused_ShouldReturn404()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var uploadUrl = started.GetProperty("uploadUrl").GetString()!;
        var first = ImageSeed.Bytes("image/png");

        using var firstPut = await ImageSeed.PutAsync(Factory, uploadUrl, first);
        using var replay = await ImageSeed.PutAsync(Factory, uploadUrl, ImageSeed.Bytes("image/png"));

        firstPut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        replay.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await replay.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Upload not found");
        (await Client.GetByteArrayAsync($"/api/images/{started.GetProperty("image").GetString()}")).ShouldBe(first);
    }

    [Test]
    public async Task Put_WhenTokenUnknown_ShouldReturn404()
    {
        using var put = await ImageSeed.PutAsync(Factory, UPLOAD_URL_PREFIX + new string('A', 43), ImageSeed.Bytes("image/png"));

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await put.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Upload not found");
    }

    [Test]
    public async Task Put_WhenTwoConcurrent_ShouldStoreOnce()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var uploadUrl = started.GetProperty("uploadUrl").GetString()!;
        var bodies = new[] { ImageSeed.Bytes("image/png", 200_000), ImageSeed.Bytes("image/png", 200_000) };

        var responses = await Task.WhenAll(bodies.Select(body => ImageSeed.PutAsync(Factory, uploadUrl, body)));

        responses.Select(r => r.StatusCode).OrderBy(s => s).ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.NotFound]);
        var winner = bodies[Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.NoContent)];
        (await Client.GetByteArrayAsync($"/api/images/{started.GetProperty("image").GetString()}")).ShouldBe(winner);
        Directory.GetFiles(Path.Combine(DataPath(), "images", deck)).Length.ShouldBe(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Test]
    public async Task Put_WhenExpired_ShouldReturn404()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);

        Clock.Advance(TimeSpan.FromSeconds(601));
        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, ImageSeed.Bytes("image/png"));

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await put.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Upload not found");
        (await Client.GetAsync($"/api/images/{started.GetProperty("image").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Refused before the token is taken, so the agent can PUT the right bytes to the same URL.
    [Test]
    public async Task Put_WhenTypeMismatch_ShouldReturn400()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var uploadUrl = started.GetProperty("uploadUrl").GetString()!;
        var image = started.GetProperty("image").GetString()!;

        using var jpeg = await ImageSeed.PutAsync(Factory, uploadUrl, ImageSeed.Bytes("image/jpeg"), "image/jpeg");

        jpeg.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await jpeg.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("The body is not a PNG image, the type upload_image declared");
        (await Client.GetAsync($"/api/images/{image}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Directory.Exists(Path.Combine(DataPath(), "images", deck)).ShouldBeFalse();
        using var retry = await ImageSeed.PutAsync(Factory, uploadUrl, ImageSeed.Bytes("image/png"));
        retry.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // Every declared type checks its own magic bytes, so another type's bytes (or a RIFF file that isn't WebP) are refused
    // and the same URL still takes the right bytes.
    [TestCase("image/jpeg", "image/png", "JPEG")]
    [TestCase("image/jpeg", "image/gif", "JPEG")]
    [TestCase("image/webp", "image/png", "WebP")]
    [TestCase("image/webp", "riff-wave", "WebP")]
    [TestCase("image/gif", "image/jpeg", "GIF")]
    [TestCase("image/gif", "gif-no-version", "GIF")]
    public async Task Put_WhenWrongBytesForDeclaredType_ShouldReturn400(string declared, string sent, string label)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck, contentType: declared);
        var uploadUrl = started.GetProperty("uploadUrl").GetString()!;
        byte[] body = sent switch
        {
            "riff-wave" => [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8, .. new byte[64]],
            "gif-no-version" => [.. "GIF8"u8, .. new byte[64]],
            _ => ImageSeed.Bytes(sent),
        };

        using var wrong = await ImageSeed.PutAsync(Factory, uploadUrl, body, declared);

        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe($"The body is not a {label} image, the type upload_image declared");
        (await Client.GetAsync($"/api/images/{started.GetProperty("image").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Directory.Exists(Path.Combine(DataPath(), "images", deck)).ShouldBeFalse();
        using var retry = await ImageSeed.PutAsync(Factory, uploadUrl, ImageSeed.Bytes(declared), declared);
        retry.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // An upload token works only while its deck's owner is approved: once an admin revokes the presenter, the URL their
    // agent was given stops working.
    [Test]
    public async Task Put_WhenPresenterRevokedAfterUploadImage_ShouldReturn404()
    {
        var pat = TestUser.NewPending();
        Guid patId;
        using (var scope = Factory.Services.CreateScope())
        {
            patId = (await TestPeople.SignInAsync(scope.ServiceProvider, pat)).Id;
        }
        using var admin = ClientFor(TestUser.Ada);
        (await admin.PostAsync($"/api/admin/users/{patId}/approve", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, pat);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);

        (await admin.PostAsync($"/api/admin/users/{patId}/revoke", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, ImageSeed.Bytes("image/png"));

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await put.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Upload not found");
        Directory.Exists(Path.Combine(DataPath(), "images", deck)).ShouldBeFalse();
    }

    [Test]
    public async Task Put_WhenSvg_ShouldReturn400()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>"""u8.ToArray();

        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, svg, "image/svg+xml");

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await put.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("The body is not a PNG image, the type upload_image declared");
        (await Client.GetAsync($"/api/images/{started.GetProperty("image").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Put_WhenEmpty_ShouldReturn400()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);

        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, []);

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Put_WhenOver10Mb_ShouldReturn413()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var uploadUrl = started.GetProperty("uploadUrl").GetString()!;

        using var tooBig = await ImageSeed.PutAsync(Factory, uploadUrl, ImageSeed.Bytes("image/png", TEN_MB + 1));
        using var atLimit = await ImageSeed.PutAsync(Factory, uploadUrl, ImageSeed.Bytes("image/png", TEN_MB));

        tooBig.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await tooBig.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("An image may be at most 10 MB");
        atLimit.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Directory.GetFiles(Path.Combine(DataPath(), "images", deck)).ShouldHaveSingleItem();
    }

    // A body with no Content-Length (chunked) is counted as it arrives and refused once it passes 10 MB.
    [Test]
    public async Task Put_WhenOver10MbWithoutLength_ShouldReturn413()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, started.GetProperty("uploadUrl").GetString())
        {
            Content = new UnsizedContent(ImageSeed.Bytes("image/png", TEN_MB + 1)),
        };

        using var put = await http.SendAsync(request);

        put.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        Directory.Exists(Path.Combine(DataPath(), "images", deck)).ShouldBeFalse();
    }

    // The guard still refuses a write another site could make a browser send.
    [Test]
    public async Task Put_WhenForeignOrigin_ShouldReturn403()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, started.GetProperty("uploadUrl").GetString()) { Content = new ByteArrayContent(ImageSeed.Bytes("image/png")) };
        request.Headers.Add("Origin", "https://evil.example");

        using var put = await http.SendAsync(request);

        put.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The upload URL needs no sign-in, but a signed-in caller writing with neither an Origin nor Sec-Fetch-Site is
    // refused by the guard like any other /api write, so a browser's cookie never carries an upload.
    [Test]
    public async Task Put_WhenSignedInWithoutOrigin_ShouldReturn403()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        using var signedIn = ClientFor(TestUser.Sam);
        signedIn.DefaultRequestHeaders.Remove("Origin");
        using var content = new ByteArrayContent(ImageSeed.Bytes("image/png"));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        using var put = await signedIn.PutAsync(started.GetProperty("uploadUrl").GetString(), content);

        put.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.GetAsync($"/api/images/{started.GetProperty("image").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenAnonymousAndPrivateDeck_ShouldReturn404()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var alex = ClientFor(TestUser.Alex);

        using var anonymousResponse = await anonymous.GetAsync($"/api/images/{image}");
        using var alexResponse = await alex.GetAsync($"/api/images/{image}");
        using var unknownResponse = await Client.GetAsync("/api/images/img_nOsUcHiMaG");

        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymousResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Image not found");
        alexResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await alexResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Image not found");
        unknownResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknownResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Image not found");
    }

    [Test]
    public async Task Get_ShouldSendNoCacheAndEtag()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));

        using var first = await Client.GetAsync($"/api/images/{image}");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{image}");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var again = await Client.SendAsync(request);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Read raw before anything parses it: the parsed header re-serialises in its own order.
        first.Headers.NonValidated["Cache-Control"].ToString().ShouldBe("private, no-cache");
        first.Headers.ETag!.Tag.ShouldStartWith($"\"{image}.", Case.Sensitive);
        again.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        again.Headers.NonValidated["Cache-Control"].ToString().ShouldBe("private, no-cache");
    }

    // A file that is also valid HTML or script (a polyglot) opened straight in a tab can run nothing: the CSP allows no
    // source and sandboxes the document.
    [Test]
    public async Task Get_ShouldSendSandboxingContentSecurityPolicy()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/gif", 512), "image/gif");

        using var response = await Client.GetAsync($"/api/images/{image}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.NonValidated["Content-Security-Policy"].ToString().ShouldBe("default-src 'none'; sandbox");
        response.Headers.NonValidated["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/gif");
    }

    [Test]
    public async Task AddSlide_WhenUploadedImage_ShouldAccept()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.SamplePng());

        var added = await DeckSeed.AddSlideAsync(agent, deck, "image", $$"""{ "headline": "The board", "image": { "image": "{{image}}", "alt": "A taskboard" } }""");

        added.GetProperty("slide").GetInt32().ShouldBe(1);
        var slots = (await SlideCalls.SlidesAsync(agent, deck))[0].GetProperty("slots");
        slots.GetProperty("image").GetProperty("image").GetString().ShouldBe(image);
    }

    // An image from another deck is not this deck's, even when its owner is the same.
    [Test]
    public async Task AddSlide_WhenImageFromAnotherDeck_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var other = await DeckSeed.CreateDeckAsync(agent, "Other");
        var image = await ImageSeed.UploadAsync(agent, Factory, other, ImageSeed.Bytes("image/png"));

        var error = await AddImageSlideErrorAsync(agent, deck, image);

        error.ShouldBe($"image.image: unknown image {image}");
    }

    [Test]
    public async Task AddSlide_WhenUploadNeverCompleted_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = (await ImageSeed.StartAsync(agent, deck)).GetProperty("image").GetString()!;

        var error = await AddImageSlideErrorAsync(agent, deck, image);

        error.ShouldBe($"image.image: unknown image {image}");
        (await SlideCalls.SlidesAsync(agent, deck)).ShouldBeEmpty();
    }

    private static async Task<string> AddImageSlideErrorAsync(McpTestClient agent, string deck, string image)
    {
        using var slots = JsonDocument.Parse($$"""{ "headline": "The board", "image": { "image": "{{image}}", "alt": "A taskboard" } }""");
        return await SlideCalls.ErrorAsync(agent, "add_slide", new { deck, layout = "image", slots = slots.RootElement });
    }

    private string DataPath() => Factory.Services.GetRequiredService<IConfiguration>()["SLIDEWORKS_DATA_PATH"]!;
}
