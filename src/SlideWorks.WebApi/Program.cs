using System.Reflection;
using System.Text.Json.Serialization;
using Agoda.IoC.NetCore;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure.Identity;
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
builder.Services.AutoWireAssembly([ApiMarker.Assembly], isMockMode: false);
// Framework registration: the AGENTS.md "no services.Add* in Program.cs" rule is
// about our own types. Tests swap in a FakeTimeProvider.
builder.Services.AddSingleton(TimeProvider.System);
// Realtime: DeckHub, fed by IDeckEvents through DeckEventsRelay (Realtime/).
builder.Services.AddSignalR();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
// Authentication and MCP each live in their own setup file (Infrastructure/Setup/).
builder.Services.AddSlideWorksAuth();
builder.Services.AddSlideWorksMcp();

var app = builder.Build();

// Fail at startup, not on the first link, when SLIDEWORKS_PUBLIC_URL is missing outside Development.
app.Services.GetRequiredService<IPublicUrls>();

// Single-instance app: apply migrations on startup, no orchestration needed.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseSlideWorksAuth();

// SPA assets ship inside wwwroot (built there by src/SlideWorks.Client). Static files
// sit behind the fail-closed check but outside the /api token guard: the shell is
// public, the data it fetches is not.
app.UseStaticFiles();

// The Docker build stamps the commit into the informational version
// (-p:SourceRevisionId=$SOURCE_COMMIT, "1.0.0+<sha>"), so /health names the
// running build rather than a static 1.0.0 every image shares.
var version = ApiMarker.Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
app.MapGet("/health", () => Results.Ok(new { status = "ok", version }));
app.MapControllers();
app.MapSlideWorksMcp();
// WebSockets only: a long-polling connection would carry the token in the URL of
// every poll. Live updates for decks; see Realtime/DeckHub.cs.
app.MapHub<DeckHub>(DeckHub.PATH, options => options.Transports = HttpTransportType.WebSockets);
// Unknown routes fall back to the SPA for client-side routing, except under
// /api and /hubs, which must keep returning 404 rather than the SPA shell.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallback("/hubs/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();
