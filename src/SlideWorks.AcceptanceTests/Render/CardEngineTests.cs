using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Render;

// The server's card engine for real (L9, D11): the running app's ICardRenderer drives the built SPA's /card routes in
// headless Chromium and returns 1200x630 PNGs. Decks go in the way an agent makes them (create_deck, set_sharing over
// MCP). The timeout and restart cases build their own PlaywrightRenderEngine against the same running app, so their
// browser and settings are their own.
[TestFixture]
public class CardEngineTests
{
    private const string SCOPE = "decks";
    private const string REDIRECT = "http://127.0.0.1:33419/callback";

    // A 1x1 PNG, for the logo.
    private static readonly byte[] LOGO = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private static ICardRenderer Cards => AcceptanceApp.Services.GetRequiredService<ICardRenderer>();

    [Test]
    public async Task Engine_CardIs1200x630()
    {
        var deck = await CreateLinkDeckAsync("cobalt");

        var card = await Cards.RenderCardAsync(deck, CancellationToken.None);

        SizeOf(card.Png).ShouldBe((PlaywrightRenderEngine.WIDTH, PlaywrightRenderEngine.HEIGHT));
    }

    [Test]
    public async Task Engine_DecksInDifferentPalettesGiveDifferentCards()
    {
        var cobalt = await Cards.RenderCardAsync(await CreateLinkDeckAsync("cobalt"), CancellationToken.None);
        var ember = await Cards.RenderCardAsync(await CreateLinkDeckAsync("ember"), CancellationToken.None);

        ember.Png.ShouldNotBe(cobalt.Png);
    }

    [Test]
    public async Task Engine_CardWithALogoRenders()
    {
        var deck = await CreateLinkDeckWithALogoAsync();

        var card = await Cards.RenderCardAsync(deck, CancellationToken.None);

        SizeOf(card.Png).ShouldBe((PlaywrightRenderEngine.WIDTH, PlaywrightRenderEngine.HEIGHT));
    }

    // Production: the engine's page lives on the loopback address (or SLIDEWORKS_RENDER_URL), while PublicOriginMiddleware
    // rewrites every request to SLIDEWORKS_PUBLIC_URL, so the page's origin is never the public one. Here the engine
    // dials render.localhost, which is neither the public host (127.0.0.1) nor the join host (localhost, which redirects
    // /card/{id} to the public URL), and a link deck with a logo still renders.
    [Test]
    public async Task Engine_CardRendersWhenTheRenderUrlIsNotThePublicUrl()
    {
        var deck = await CreateLinkDeckWithALogoAsync();
        await using var browser = new RenderBrowser();
        using var engine = EngineWith(browser, RenderOptions.DEFAULT_TIMEOUT, AcceptanceApp.BaseUrl.Replace("127.0.0.1", "render.localhost", StringComparison.Ordinal));

        var png = await engine.RenderCardAsync(deck, CancellationToken.None);

        SizeOf(png).ShouldBe((PlaywrightRenderEngine.WIDTH, PlaywrightRenderEngine.HEIGHT));
    }

    // A navigation that fails (here, a render URL nobody listens on) is "Rendering failed: page error", and nothing in
    // the exception carries the render token, which Playwright's own message and call log include in the URL.
    [Test]
    public async Task Engine_UnreachableRenderUrlFailsWithoutTheToken()
    {
        var deck = await CreateLinkDeckAsync("cobalt");
        await using var browser = new RenderBrowser();
        using var engine = EngineWith(browser, RenderOptions.DEFAULT_TIMEOUT, $"http://127.0.0.1:{ClosedPort()}");

        var error = await Should.ThrowAsync<RenderingFailedException>(() => engine.RenderCardAsync(deck, CancellationToken.None));

        error.Message.ShouldStartWith(PlaywrightRenderEngine.FAILED);
        error.InnerException.ShouldBeNull();
        var text = error.ToString();
        text.ShouldNotContain("#token=");
        text.ShouldNotContain("token=");
        var tokens = AcceptanceApp.Services.GetRequiredService<IRenderTokens>();
        System.Text.RegularExpressions.Regex.Matches(text, "[A-Za-z0-9_.~=+/-]{16,}")
            .Select(match => match.Value)
            .Where(candidate => tokens.Validate(candidate, deck, RenderPurpose.Card))
            .ShouldBeEmpty("no live Card token for the deck may appear in the exception");
    }

    [Test]
    public async Task Engine_GenericCardRenders()
    {
        var card = await Cards.RenderGenericCardAsync(CancellationToken.None);

        card.Key.ShouldBe(RenderKey.GENERIC);
        SizeOf(card.Png).ShouldBe((PlaywrightRenderEngine.WIDTH, PlaywrightRenderEngine.HEIGHT));
    }

    [Test]
    public async Task Engine_RenderTimesOut()
    {
        await using var browser = new RenderBrowser();
        using var engine = EngineWith(browser, TimeSpan.FromMilliseconds(1));

        var error = await Should.ThrowAsync<TimeoutException>(() => engine.RenderGenericCardAsync(CancellationToken.None));

        error.Message.ShouldBe(PlaywrightRenderEngine.TIMED_OUT);
    }

    [Test]
    public async Task Engine_BrowserRestartsAfterItDies()
    {
        await using var browser = new RenderBrowser();
        using var engine = EngineWith(browser, RenderOptions.DEFAULT_TIMEOUT);
        var before = ChromiumBrowsers();
        await engine.RenderGenericCardAsync(CancellationToken.None);
        var ours = ChromiumBrowsers().Except(before).ToList();
        ours.ShouldNotBeEmpty("the engine's Chromium should be a child of this test process");

        var killed = 0;
        foreach (var pid in ours)
        {
            killed += await KillIfRunningAsync(pid) ? 1 : 0;
        }
        killed.ShouldBePositive("the engine's Chromium should still be running before it is killed");
        var png = await engine.RenderGenericCardAsync(CancellationToken.None);

        SizeOf(png).ShouldBe((PlaywrightRenderEngine.WIDTH, PlaywrightRenderEngine.HEIGHT));
    }

    private static async Task<bool> KillIfRunningAsync(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            await process.WaitForExitAsync();
            return true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static PlaywrightRenderEngine EngineWith(RenderBrowser browser, TimeSpan timeout, string? renderUrl = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RenderOptions.URL_SETTING] = renderUrl ?? AcceptanceApp.BaseUrl,
            [RenderOptions.TIMEOUT_SETTING] = timeout.ToString("c"),
        }).Build();
        return new PlaywrightRenderEngine(
            browser,
            AcceptanceApp.Services.GetRequiredService<IRenderTokens>(),
            configuration,
            AcceptanceApp.Services.GetRequiredService<IServer>(),
            AcceptanceApp.Services.GetRequiredService<IPublicUrls>());
    }

    // The width and height from a PNG's IHDR chunk.
    private static (int Width, int Height) SizeOf(byte[] png)
    {
        png.AsSpan(0, 8).ToArray().ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    // Main Chromium processes (no --type= switch) descended from this test process.
    private static HashSet<int> ChromiumBrowsers()
    {
        var parents = new Dictionary<int, int>();
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (int.TryParse(Path.GetFileName(dir), out var pid) && TryReadParent(dir, out var parent))
            {
                parents[pid] = parent;
            }
        }
        return [.. parents.Keys.Where(pid => DescendsFromUs(pid, parents) && IsChromiumBrowser(pid))];
    }

    private static bool TryReadParent(string dir, out int parent)
    {
        parent = 0;
        try
        {
            var stat = File.ReadAllText(Path.Combine(dir, "stat"));
            return int.TryParse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[1], out parent);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool DescendsFromUs(int pid, Dictionary<int, int> parents)
    {
        var self = Environment.ProcessId;
        for (var current = pid; parents.TryGetValue(current, out var parent) && parent > 1; current = parent)
        {
            if (parent == self)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsChromiumBrowser(int pid)
    {
        try
        {
            var commandLine = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0');
            var program = Path.GetFileName(commandLine[0]);
            return (program.Contains("chrom", StringComparison.OrdinalIgnoreCase) || program.Contains("headless_shell", StringComparison.Ordinal))
                && !commandLine.Any(arg => arg.StartsWith("--type=", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return false;
        }
    }

    // A loopback port that was free a moment ago and has nothing listening on it now.
    private static int ClosedPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> CreateLinkDeckWithALogoAsync()
    {
        var deck = await CreateLinkDeckAsync("cobalt");
        await using var agent = await ConnectAsync(TestUser.Sam);
        var upload = await CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "logo.png", ["contentType"] = "image/png" });
        using var http = new HttpClient();
        using var body = new ByteArrayContent(LOGO);
        body.Headers.ContentType = new("image/png");
        (await http.PutAsync(upload.GetProperty("uploadUrl").GetString(), body)).EnsureSuccessStatusCode();
        await CallAsync(agent, "set_look", new() { ["deck"] = deck, ["logo"] = upload.GetProperty("image").GetString() });
        return deck;
    }

    private static async Task<string> CreateLinkDeckAsync(string palette)
    {
        await using var agent = await ConnectAsync(TestUser.Sam);
        var created = await CallAsync(agent, "create_deck", new() { ["title"] = $"Ship smaller in {palette}", ["palette"] = palette });
        var deck = created.GetProperty("deck").GetString()!;
        await CallAsync(agent, "set_sharing", new() { ["deck"] = deck, ["level"] = "link" });
        return deck;
    }

    private static async Task<JsonElement> CallAsync(McpClient agent, string tool, Dictionary<string, object?> arguments)
    {
        var result = await agent.CallToolAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        (result.IsError ?? false).ShouldBeFalse($"{tool} failed: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // An agent's MCP connection, from the seams an OAuth flow ends in, as AgentSeed makes one.
    private static async Task<McpClient> ConnectAsync(TestUser presenter)
    {
        using var scope = AcceptanceApp.Services.CreateScope();
        var services = scope.ServiceProvider;
        var person = await TestPeople.SignInAsync(services, presenter);
        var oauthClient = await services.GetRequiredService<IOAuthClients>().RegisterAsync(new ClientRegistration("Claude", [REDIRECT]));
        var mcpUrl = services.GetRequiredService<IPublicUrls>().Mcp;
        var tokens = await services.GetRequiredService<IAgentTokens>().IssueAsync(new CodeGrant(oauthClient.ClientId, person.Id, SCOPE, mcpUrl));
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(mcpUrl),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {tokens.AccessToken}" },
        });
        return await McpClient.CreateAsync(transport);
    }
}
