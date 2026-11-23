using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts.Profile;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Identity.Google;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Profile;

// Photo upload and remove (C3, D4, D21, D28). Each test is its own fresh approved person, signed in through the Google
// flow so a Google picture can be part of the story (the test scheme signs in without one).
[TestFixture]
public class ProfilePhotoTests : GoogleSignInFixture
{
    private const string GOOGLE_PICTURE = "https://images.example/google.png";

    [Test]
    public async Task PostPhoto_WhenPng_ShouldServeItAndChangeAvatar()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;
        var bytes = ImageSeed.SamplePng();

        using var response = await PostAsync(browser, bytes, "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(profile, "profile.json", ScreenShapes.Profile);
        var pictureUrl = profile.GetProperty("identity").GetProperty("avatar").GetProperty("pictureUrl").GetString()!;
        pictureUrl.ShouldStartWith($"/api/people/{person}/photo/");
        profile.GetProperty("identity").GetProperty("hasPhoto").GetBoolean().ShouldBeTrue();
        (await MeAsync(browser)).GetProperty("avatar").GetProperty("pictureUrl").GetString().ShouldBe(pictureUrl);
        using var get = await browser.GetAsync(pictureUrl);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        get.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        get.Headers.CacheControl!.ToString().ShouldBe("max-age=31536000, private, immutable");
        get.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        (await get.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    [TestCase("image/jpeg")]
    [TestCase("image/webp")]
    public async Task PostPhoto_WhenJpegOrWebp_ShouldStoreItWithItsType(string contentType)
    {
        var (browser, _) = await SignedInAsync();
        using var _b = browser;
        var bytes = contentType == "image/jpeg" ? Jpeg(800, 600) : Webp(800, 600);

        using var response = await PostAsync(browser, bytes, contentType);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var pictureUrl = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identity").GetProperty("avatar").GetProperty("pictureUrl").GetString()!;
        using var get = await browser.GetAsync(pictureUrl);
        get.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
    }

    [Test]
    public async Task PostPhoto_WhenGif_ShouldReturn400()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;

        using var response = await PostAsync(browser, "GIF89a"u8.ToArray().Concat(new byte[32]).ToArray(), "image/gif");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AvatarUrlAsync(browser)).ShouldBe(GOOGLE_PICTURE);
        Directory.Exists(Path.Combine(AvatarsPath(), person.ToString("D"))).ShouldBeFalse();
    }

    [Test]
    public async Task PostPhoto_WhenBytesDontMatchType_ShouldReturn400()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;

        using var response = await PostAsync(browser, ImageSeed.SamplePng(), "image/jpeg");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AvatarUrlAsync(browser)).ShouldBe(GOOGLE_PICTURE);
        Directory.Exists(Path.Combine(AvatarsPath(), person.ToString("D"))).ShouldBeFalse();
    }

    [Test]
    public async Task PostPhoto_WhenOver5Mb_ShouldReturn413()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;
        var bytes = new byte[AvatarFiles.MAX_BYTES + 1];
        ImageSeed.SamplePng().CopyTo(bytes, 0);

        using var response = await PostAsync(browser, bytes, "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await AvatarUrlAsync(browser)).ShouldBe(GOOGLE_PICTURE);
        Directory.Exists(Path.Combine(AvatarsPath(), person.ToString("D"))).ShouldBeFalse();
    }

    [Test]
    public async Task PostPhoto_WhenOver2048Px_ShouldReturn400()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;
        var bytes = ImageSeed.SamplePng();
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), 2049);

        using var response = await PostAsync(browser, bytes, "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AvatarUrlAsync(browser)).ShouldBe(GOOGLE_PICTURE);
        Directory.Exists(Path.Combine(AvatarsPath(), person.ToString("D"))).ShouldBeFalse();
    }

    [Test]
    public async Task PostPhoto_WhenExactly2048Px_ShouldAccept()
    {
        var (browser, _) = await SignedInAsync();
        using var _b = browser;

        using var response = await PostAsync(browser, Jpeg(2048, 2048), "image/jpeg");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task PostPhoto_WhenHeaderUnreadable_ShouldReturn400()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;
        var truncated = ImageSeed.SamplePng().Take(14).ToArray();

        using var response = await PostAsync(browser, truncated, "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldContain("damaged image");
        (await AvatarUrlAsync(browser)).ShouldBe(GOOGLE_PICTURE);
        Directory.Exists(Path.Combine(AvatarsPath(), person.ToString("D"))).ShouldBeFalse();
    }

    [Test]
    public async Task DeletePhoto_WhenGooglePicture_ShouldFallBackToGoogle()
    {
        var (browser, person) = await SignedInAsync(picture: GOOGLE_PICTURE);
        using var _ = browser;
        using var uploaded = await PostAsync(browser, ImageSeed.SamplePng(), "image/png");
        uploaded.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await browser.DeleteAsync("/api/me/photo");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var identity = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identity");
        identity.GetProperty("hasPhoto").GetBoolean().ShouldBeFalse();
        identity.GetProperty("avatar").GetProperty("pictureUrl").GetString().ShouldBe(GOOGLE_PICTURE);
        Directory.GetFiles(Path.Combine(AvatarsPath(), person.ToString("D"))).Length.ShouldBe(1);
    }

    [Test]
    public async Task DeletePhoto_WhenNoGooglePicture_ShouldFallBackToInitials()
    {
        var (browser, _) = await SignedInAsync();
        using var _b = browser;
        using var uploaded = await PostAsync(browser, ImageSeed.SamplePng(), "image/png");
        uploaded.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await browser.DeleteAsync("/api/me/photo");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var avatar = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identity").GetProperty("avatar");
        avatar.GetProperty("pictureUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        avatar.GetProperty("initials").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task GetPhoto_WhenOldPhotoId_ShouldReturn404()
    {
        var (browser, _) = await SignedInAsync();
        using var _b = browser;
        using var first = await PostAsync(browser, ImageSeed.SamplePng(), "image/png");
        var oldUrl = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identity").GetProperty("avatar").GetProperty("pictureUrl").GetString()!;
        using var second = await PostAsync(browser, Jpeg(64, 64), "image/jpeg");
        var newUrl = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("identity").GetProperty("avatar").GetProperty("pictureUrl").GetString()!;

        using var old = await browser.GetAsync(oldUrl);
        using var current = await browser.GetAsync(newUrl);

        newUrl.ShouldNotBe(oldUrl);
        old.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task PostPhoto_WhenPending_ShouldReturn403()
    {
        using var pending = ClientFor(TestUser.NewPending());

        using var response = await PostAsync(pending, ImageSeed.SamplePng(), "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    [Test]
    public void SizeOf_WhenJpegHasSegmentsBeforeFrame_ShouldSkipThem()
    {
        var withApp = Jpeg(300, 200, appBytes: 500);

        var size = PhotoHeader.SizeOf("image/jpeg", withApp);

        size.ShouldBe((300, 200));
    }

    [Test]
    public void SizeOf_WhenJpegHasNoFrame_ShouldBeNull()
    {
        var size = PhotoHeader.SizeOf("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xD9 });

        size.ShouldBeNull();
    }

    private async Task<(HttpClient Browser, Guid UserId)> SignedInAsync(string? picture = null)
    {
        var person = TestUser.NewApproved();
        Guid id;
        using (var scope = Factory.Services.CreateScope())
        {
            id = (await TestPeople.SignInAsync(scope.ServiceProvider, person)).Id;
        }
        var browser = Browser();
        using var response = await SignInAsync(browser, person.GoogleSubject, person.Email, person.Name, picture: picture);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return (browser, id);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return client.PostAsync("/api/me/photo", content);
    }

    private static async Task<string?> AvatarUrlAsync(HttpClient browser)
        => (await MeAsync(browser)).GetProperty("avatar").GetProperty("pictureUrl").GetString();

    private string AvatarsPath() => Path.Combine(Factory.Services.GetRequiredService<IConfiguration>()["SLIDEWORKS_DATA_PATH"]!, "avatars");

    // A minimal JPEG: SOI, optional APP0 padding, a baseline start-of-frame, EOI.
    private static byte[] Jpeg(int width, int height, int appBytes = 0)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        if (appBytes > 0)
        {
            bytes.AddRange([0xFF, 0xE1, (byte)((appBytes + 2) >> 8), (byte)((appBytes + 2) & 0xFF)]);
            bytes.AddRange(new byte[appBytes]);
        }
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)(height & 0xFF), (byte)(width >> 8), (byte)(width & 0xFF), 0x01, 0x01, 0x11, 0x00]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    // A minimal extended WebP: a VP8X chunk holding the canvas size.
    private static byte[] Webp(int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes(22));
        bytes.AddRange("WEBPVP8X"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes(10));
        bytes.AddRange(new byte[4]);
        bytes.AddRange(BitConverter.GetBytes(width - 1).Take(3));
        bytes.AddRange(BitConverter.GetBytes(height - 1).Take(3));
        return [.. bytes];
    }
}
