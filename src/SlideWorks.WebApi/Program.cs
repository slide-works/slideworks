using System.Reflection;
using System.Text.Json.Serialization;
using Agoda.IoC.NetCore;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi;
using SlideWorks.WebApi.Data;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Infrastructure.Setup;
using SlideWorks.WebApi.Realtime;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog replaces the logger factory, so levels live under Serilog:MinimumLevel
// rather than Logging:LogLevel. The console sink keeps `docker logs` working;
// Seq is posted to directly only when SLIDEWORKS_SEQ_URL is set, so tests and local
// dev never reach for it. Configured from the service provider, not here, because
// WebApplicationFactory-based tests append their config overrides after this
// top-level code has run.
// preserveStaticLogger keeps each host's logger its own, so one test host being
// disposed can't close the static logger another is still writing through.
builder.Services.AddSerilog((services, logger) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    logger.ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "slide-works")
        .WriteTo.Console();
    var seqUrl = configuration["SLIDEWORKS_SEQ_URL"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        logger.WriteTo.Seq(seqUrl, apiKey: configuration["SLIDEWORKS_SEQ_API_KEY"]);
    }
}, preserveStaticLogger: true);

// Resolve the path at runtime, not builder time, for the same reason.
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
{
    var dbPath = provider.GetRequiredService<IConfiguration>()["SLIDEWORKS_DB_PATH"] ?? "/data/slide-works.db";
    options.UseSqlite($"Data Source={dbPath}");
});
builder.Services.AutoWireAssembly([ApiMarker.Assembly], isMockMode: false,
    options => options.OnRegistrationContextException = ex => Console.Error.WriteLine(ex.ToString()));
// Framework registration: the AGENTS.md "no services.Add* in Program.cs" rule is
// about our own types. Tests swap in a FakeTimeProvider.
builder.Services.AddSingleton(TimeProvider.System);
// Realtime: DeckHub, fed by IDeckEvents through DeckEventsRelay (Realtime/).
// The hub sends and reads enums as camelCase strings, as the MVC options below do (SW-4 P1).
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.AddHttpContextAccessor();
// Authentication and MCP each live in their own setup file (Infrastructure/Setup/).
builder.Services.AddSlideWorksAuth();
builder.Services.AddSlideWorksMcp();
// Render tokens (L9): Core's RenderTokens with its key from configuration.
builder.Services.AddSlideWorksRendering();

var app = builder.Build();

// Fail at startup, not on the first link, when SLIDEWORKS_PUBLIC_URL is missing outside Development.
app.Services.GetRequiredService<IPublicUrls>();
// The same for the render token key (RenderTokens:Key).
app.Services.GetRequiredService<IRenderTokens>();

// Before the migrations, so a bad SLIDEWORKS_TRUSTED_PROXIES entry stops startup without touching the database. Read from
// app.Configuration, which holds every override by now.
var trustedProxies = TrustedProxiesSetup.Parse(app.Configuration[TrustedProxiesSetup.CONFIG_KEY]);

// Single-instance app: apply migrations on startup, no orchestration needed.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// First, so the rate limits see the client's address behind a trusted proxy (SLIDEWORKS_TRUSTED_PROXIES, opt-in).
app.UseSlideWorksTrustedProxies(trustedProxies);

// Then the body limit for /mcp and /api, before anything reads a body.
app.UseSlideWorksRequestLimits();

// SPA assets ship inside wwwroot (built there by src/SlideWorks.Client). Static files
// come before authentication: a request for a file matches no endpoint, so a fallback
// "must be signed in" policy would otherwise refuse the anonymous player its bundle.
// The shell is public, the data it fetches is not.
app.UseStaticFiles();

app.UseSlideWorksAuth();

// The Docker build stamps the commit into the informational version
// (-p:SourceRevisionId=$SOURCE_COMMIT, "1.0.0+<sha>"), so /health names the
// running build rather than a static 1.0.0 every image shares.
var version = ApiMarker.Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
app.MapGet("/health", () => Results.Ok(new { status = "ok", version })).AllowAnonymous();
app.MapControllers();
app.MapSlideWorksMcp();
// Live updates for decks and the audience: WebSockets, then Server-Sent Events, then long polling for guests on networks
// that block the first (D7); see Realtime/DeckHub.cs.
app.MapHub<DeckHub>(DeckHub.PATH, options => options.Transports =
    HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling);
// Unknown routes fall back to the SPA for client-side routing, except under
// /api and /hubs, which must keep returning 404 rather than the SPA shell. The shell
// is anonymous: the player (/d/:id/:slug) opens for viewers who never sign in.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallback("/hubs/{**path}", () => Results.NotFound());
app.MapGet("/d/{id}/{slug?}", (string id, SlideWorks.WebApi.Features.Render.PlayerPageHtml page) => page.ServeAsync(id)).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();
