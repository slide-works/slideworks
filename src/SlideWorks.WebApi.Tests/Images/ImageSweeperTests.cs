using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Images;

// The sweeper (H3): temp files in images/.uploads that nobody is writing and image rows whose upload expired without
// completing are removed an hour after they went stale. The sweep runs on the app's TimeProvider timer, so each test steps
// the fake clock past the hour. An expired upload answers 404 whether or not its row is still there, so the rows are
// observed with a read-only query through the app's own DbContext; every row is still made through the public routes.
[TestFixture]
[CancelAfter(60000)]
public class ImageSweeperTests : ApiTestFixture
{
    private static readonly TimeSpan SWEEP_STEP = TimeSpan.FromHours(2);

    // A file last written an hour and a half ago is past the grace once the clock is two hours on; ten minutes ago is not.
    private static readonly TimeSpan STRAY_AGE = TimeSpan.FromHours(3);
    private static readonly TimeSpan RECENT_AGE = TimeSpan.FromMinutes(10);

    [Test]
    public async Task Sweep_WhenTempFilesOldAndRecent_ShouldRemoveOnlyTheOldOne()
    {
        var stray = TempFile("stray", Clock.GetUtcNow() + SWEEP_STEP - STRAY_AGE);
        var recent = TempFile("recent", Clock.GetUtcNow() + SWEEP_STEP - RECENT_AGE);

        await SweepAsync();

        File.Exists(stray).ShouldBeFalse();
        File.Exists(recent).ShouldBeTrue();
    }

    [Test]
    public async Task Sweep_WhenUploadStillArriving_ShouldKeepItsTempFileUntilItEnds()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var before = Directory.Exists(TempFolder()) ? Directory.GetFiles(TempFolder()) : [];
        var body = new HeldBody(ImageSeed.Bytes("image/png"));
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var put = http.PutAsync(started.GetProperty("uploadUrl").GetString(), body);
        var temp = await TempFileOfAsync(before);

        await SweepAsync();

        File.Exists(temp).ShouldBeTrue();
        body.Release();
        using var response = await put;
        // The upload's own token ran out while the body was held, so it ends in a 404; either way its file is gone.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        File.Exists(temp).ShouldBeFalse();
    }

    [Test]
    public async Task Sweep_WhenUploadExpiredNeverCompleted_ShouldRemoveItsRowAndKeepCompletedImage()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var used = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        await DeckSeed.AddSlideAsync(agent, deck, "image", $$"""{ "headline": "The board", "image": { "image": "{{used}}", "alt": "A taskboard" } }""");
        using var logo = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", new { logo = used });
        logo.StatusCode.ShouldBe(HttpStatusCode.OK, await logo.Content.ReadAsStringAsync());
        var abandoned = (await ImageSeed.StartAsync(agent, deck)).GetProperty("image").GetString().ShouldNotBeNull();
        (await RowExistsAsync(abandoned)).ShouldBeTrue();

        await SweepAsync();

        (await RowExistsAsync(abandoned)).ShouldBeFalse();
        (await RowExistsAsync(used)).ShouldBeTrue();
        using var served = await Client.GetAsync($"/api/images/{used}");
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(Path.Combine(DataPath(), "images", deck, used)).ShouldBeTrue();
    }

    [Test]
    public async Task Sweep_WhenTempFileWrittenButNotReleased_ShouldKeepItUntilReleased()
    {
        var store = Factory.Services.GetRequiredService<ImageStore>();
        using var body = new MemoryStream(ImageSeed.Bytes("image/png"));
        // Written and checked but not yet moved into place or deleted: the stretch of a request after its body is in.
        var (temp, _) = await store.WriteTempAsync(body, CancellationToken.None);
        File.SetLastWriteTimeUtc(temp, (Clock.GetUtcNow() + SWEEP_STEP - STRAY_AGE).UtcDateTime);

        await SweepAsync();

        File.Exists(temp).ShouldBeTrue();
        store.ReleaseTemp(temp);
        await SweepAsync();
        File.Exists(temp).ShouldBeFalse();
    }

    [Test]
    public async Task Sweep_WhenUploadInFlightPastGrace_ShouldKeepItsRow()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var inFlight = started.GetProperty("image").GetString().ShouldNotBeNull();
        var before = Directory.Exists(TempFolder()) ? Directory.GetFiles(TempFolder()) : [];
        var body = new HeldBody(ImageSeed.Bytes("image/png"));
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var put = http.PutAsync(started.GetProperty("uploadUrl").GetString(), body);
        await TempFileOfAsync(before);

        await SweepAsync();

        (await RowExistsAsync(inFlight)).ShouldBeTrue();
        body.Release();
        using var response = await put;
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await SweepAsync();
        (await RowExistsAsync(inFlight)).ShouldBeFalse();
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public async Task Sweep_WhenFileCannotBeDeleted_ShouldKeepTheRowUntilItCan()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var stuck = (await ImageSeed.StartAsync(agent, deck)).GetProperty("image").GetString().ShouldNotBeNull();
        var folder = Path.Combine(DataPath(), "images", deck);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, stuck);
        await File.WriteAllBytesAsync(file, ImageSeed.Bytes("image/png"));
        // A folder nobody may open hides its files from File.Exists and refuses the delete of one; the row must not go.
        File.SetUnixFileMode(folder, UnixFileMode.None);
        try
        {
            await SweepAsync();

            (await RowExistsAsync(stuck)).ShouldBeTrue();
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        File.Exists(file).ShouldBeTrue();
        await SweepAsync();
        (await RowExistsAsync(stuck)).ShouldBeFalse();
        File.Exists(file).ShouldBeFalse();
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public async Task Sweep_WhenRequestCouldNotDeleteItsTempFile_ShouldSweepItOnceItCan()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);
        var before = Directory.Exists(TempFolder()) ? Directory.GetFiles(TempFolder()) : [];
        var body = new HeldBody(ImageSeed.Bytes("image/png"));
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var put = http.PutAsync(started.GetProperty("uploadUrl").GetString(), body);
        var temp = await TempFileOfAsync(before);
        // With the folder closed to writes, the request can neither move its temp file nor delete it.
        File.SetUnixFileMode(TempFolder(), UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            body.Release();
            using var response = await put;
            response.IsSuccessStatusCode.ShouldBeFalse();
            File.Exists(temp).ShouldBeTrue();
        }
        finally
        {
            File.SetUnixFileMode(TempFolder(), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        File.SetLastWriteTimeUtc(temp, (Clock.GetUtcNow() + SWEEP_STEP - STRAY_AGE).UtcDateTime);

        await SweepAsync();

        File.Exists(temp).ShouldBeFalse();
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public async Task Sweep_WhenTempFileCannotBeDeleted_ShouldStillSweepExpiredRows()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var expired = (await ImageSeed.StartAsync(agent, deck)).GetProperty("image").GetString().ShouldNotBeNull();
        var stray = TempFile("stray", Clock.GetUtcNow() + SWEEP_STEP - STRAY_AGE);
        File.SetUnixFileMode(TempFolder(), UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            await SweepAsync();

            (await RowExistsAsync(expired)).ShouldBeFalse();
        }
        finally
        {
            File.SetUnixFileMode(TempFolder(), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        File.Exists(stray).ShouldBeTrue();
        await SweepAsync();
        File.Exists(stray).ShouldBeFalse();
    }

    // Steps the clock past the grace, which fires the timer's sweep, then runs a sweep and waits for it. Sweeps never overlap,
    // so when this returns a whole sweep has run since the clock moved.
    private async Task SweepAsync()
    {
        Clock.Advance(SWEEP_STEP);
        await Factory.Services.GetServices<IHostedService>().OfType<ImageSweeper>().Single().SweepAsync(CancellationToken.None);
    }

    // Checks on every tick of a timer until it holds, or fails the test after 30 seconds.
    private static async Task WaitUntilAsync(Func<Task<bool>> holds, string failure)
    {
        using var give = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var tick = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        while (!await holds())
        {
            (await tick.WaitForNextTickAsync(give.Token)).ShouldBeTrue(failure);
        }
    }

    private async Task<bool> RowExistsAsync(string imageId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().DeckImages.AsNoTracking().AnyAsync(i => i.Id == imageId);
    }

    private string DataPath() => Factory.Services.GetRequiredService<IConfiguration>()[ImageStore.DATA_PATH_SETTING]!;

    private string TempFolder() => Path.Combine(DataPath(), "images", ".uploads");

    private string TempFile(string name, DateTimeOffset lastWrite)
    {
        Directory.CreateDirectory(TempFolder());
        var path = Path.Combine(TempFolder(), name);
        File.WriteAllBytes(path, ImageSeed.Bytes("image/png"));
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        return path;
    }

    // The temp file the server made for the held upload: the one not there before it started.
    private async Task<string> TempFileOfAsync(string[] before)
    {
        var created = new List<string>();
        await WaitUntilAsync(() =>
        {
            created = Directory.Exists(TempFolder()) ? Directory.GetFiles(TempFolder()).Except(before).ToList() : [];
            return Task.FromResult(created.Count == 1);
        }, "the server never started writing the upload");
        return created[0];
    }

    // A PNG body whose second half waits for Release, as a slow client's would.
    private sealed class HeldBody : HttpContent
    {
        private readonly byte[] _bytes;
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HeldBody(byte[] bytes)
        {
            _bytes = bytes;
            Headers.ContentType = new MediaTypeHeaderValue("image/png");
        }

        public void Release() => _released.SetResult();

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var half = _bytes.Length / 2;
            await stream.WriteAsync(_bytes.AsMemory(0, half));
            await stream.FlushAsync();
            await _released.Task;
            await stream.WriteAsync(_bytes.AsMemory(half));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
