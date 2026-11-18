using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Claiming an upload token (T6): a start removes the template's used rows, and one that begins right after a claim has
// taken its token must not make the upload vanish.
[TestFixture]
[CancelAfter(60000)]
public class TemplateAssetClaimTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";
    private const string SVG = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 4 4"><rect width="4" height="4"/></svg>""";

    private readonly StartAfterClaim _watch = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_watch));

    [Test]
    public async Task Complete_WhenAStartBeginsRightAfterTheClaim_ShouldStillStoreTheUpload()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var url = await StartAsync(template);
        Task<HttpResponseMessage>? start = null;
        _watch.OnClaim = () => start = StartTaskAsync(template);

        using var put = await PutAsync(url);
        _watch.OnClaim = null;

        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        var startTask = start ?? throw new InvalidOperationException("The claim never ran.");
        using var started = await startTask;
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        _watch.Claims.ShouldBe(1);
    }

    private async Task<string> StartAsync(string template)
    {
        using var response = await StartTaskAsync(template);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadUrl").GetString()!;
    }

    private Task<HttpResponseMessage> StartTaskAsync(string template)
        => Client.PostAsJsonAsync($"/api/templates/{template}/assets", new { kind = "logo", fileName = "mark.svg", rightsConfirmed = false });

    private async Task<HttpResponseMessage> PutAsync(string uploadUrl)
    {
        using var http = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(SVG));
        return await http.PutAsync(uploadUrl, content);
    }

    // Runs OnClaim as soon as the statement that takes an upload token has run, and gives it a moment to finish a
    // start there: one that is not held back by the claim deletes the row the claim is about to read.
    private sealed class StartAfterClaim : DbCommandInterceptor
    {
        private int _claims;

        public Action? OnClaim { get; set; }

        public int Claims => Volatile.Read(ref _claims);

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (OnClaim is { } hook && command.CommandText.StartsWith("UPDATE \"TemplateUploads\"", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _claims);
                OnClaim = null;
                hook();
                await Task.Delay(TimeSpan.FromMilliseconds(750), CancellationToken.None);
            }
            return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
