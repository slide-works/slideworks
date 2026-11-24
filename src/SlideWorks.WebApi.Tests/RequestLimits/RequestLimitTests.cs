using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.RequestLimits;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.RequestLimits;

// K6: a request body over the limit is 413 { "error" }, with or without a Content-Length, before a tool or controller
// reads any of it. /mcp and the other /api routes share one limit (1 MB); the sharing PUT is held to 64 KB; an upload
// route, which counts its own bytes against a larger cap, keeps that cap.
[TestFixture]
[CancelAfter(60000)]
public class RequestLimitTests : ApiTestFixture
{
    private const int MCP_LIMIT = 1024 * 1024;
    private const int SHARING_LIMIT = 64 * 1024;
    private const int TEN_MB = 10 * 1024 * 1024;

    [Test]
    public async Task McpCall_WhenBodyIsOverTheLimit_ShouldAnswer413AndRunNoTool()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var quote = new string('a', MCP_LIMIT);

        using var response = await PostMcpAsync(agent, AddQuote(deck, quote), chunked: false);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe($"body: at most {MCP_LIMIT} bytes");
        (await SlideCalls.SlidesAsync(agent, deck)).ShouldBeEmpty();
    }

    [Test]
    public async Task McpCall_WhenChunkedBodyIsOverTheLimit_ShouldAnswer413AndRunNoTool()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        using var response = await PostMcpAsync(agent, AddQuote(deck, new string('a', MCP_LIMIT)), chunked: true);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe($"body: at most {MCP_LIMIT} bytes");
        (await SlideCalls.SlidesAsync(agent, deck)).ShouldBeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task McpCall_WhenBodyIsExactlyTheLimit_ShouldRunTheTool(bool chunked)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        const string Call = """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"list_decks","arguments":{}}}""";

        using var response = await PostMcpAsync(agent, Call + new string(' ', MCP_LIMIT - Call.Length), chunked);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"result\"");
    }

    [Test]
    public async Task SharingPut_WhenBodyIsOverTheLimit_ShouldAnswer413AndChangeNothing()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        using var response = await PutSharingAsync(deck, SharingBody(SHARING_LIMIT + 1), chunked: false);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe($"body: at most {SHARING_LIMIT} bytes");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("private");
    }

    [Test]
    public async Task SharingPut_WhenChunkedBodyIsOverTheLimit_ShouldAnswer413AndChangeNothing()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        using var response = await PutSharingAsync(deck, SharingBody(SHARING_LIMIT + 1), chunked: true);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe($"body: at most {SHARING_LIMIT} bytes");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("private");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SharingPut_WhenBodyIsExactlyTheLimit_ShouldChangeTheSharing(bool chunked)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        using var response = await PutSharingAsync(deck, SharingBody(SHARING_LIMIT), chunked);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("level").GetString().ShouldBe("link");
    }

    [Test]
    public async Task ApiRoute_WhenBodyIsOverTheSharedLimit_ShouldAnswer413()
    {
        using var content = new StringContent($$"""{"title":"{{new string('a', MCP_LIMIT)}}"}""", Encoding.UTF8, "application/json");

        using var response = await Client.PostAsync("/api/decks", content);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe($"body: at most {RequestBodyLimitMiddleware.DEFAULT_BYTES} bytes");
    }

    [Test]
    public async Task ImageUpload_WhenExactlyTenMb_ShouldStillBeStored()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var started = await ImageSeed.StartAsync(agent, deck);

        using var put = await ImageSeed.PutAsync(Factory, started.GetProperty("uploadUrl").GetString()!, ImageSeed.Bytes("image/png", TEN_MB));

        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // HTTP/2 and HTTP/3 can send a body with neither Content-Length nor Transfer-Encoding, and TestServer cannot send that
    // shape, so the middleware is run directly over a context that has neither header (and an endpoint-less route, which
    // takes the shared limit). The body stream is counted: the middleware must stop reading near the limit.
    [TestCase("POST")]
    [TestCase("GET")]
    [TestCase("HEAD")]
    public async Task Middleware_WhenBodyHasNoLengthHeaderAtAllAndIsOverTheLimit_ShouldAnswer413AndNotCallOn(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/decks";
        context.Request.Body = new MemoryStream(new byte[MCP_LIMIT + 1]);
        context.Response.Body = new MemoryStream();
        var reachedNext = false;
        var middleware = new RequestBodyLimitMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        context.Request.ContentLength.ShouldBeNull();
        context.Request.Headers.ContainsKey("Transfer-Encoding").ShouldBeFalse();
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        reachedNext.ShouldBeFalse();
        context.Response.Body.Position = 0;
        (await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body)).GetProperty("error").GetString()
            .ShouldBe($"body: at most {MCP_LIMIT} bytes");
    }

    [Test]
    public async Task Middleware_WhenBodyHasNoLengthHeaderAtAllAndIsExactlyTheLimit_ShouldCallOnWithTheWholeBody()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/decks";
        context.Request.Body = new MemoryStream(new byte[MCP_LIMIT]);
        long seen = -1;
        var middleware = new RequestBodyLimitMiddleware(async http =>
        {
            using var copy = new MemoryStream();
            await http.Request.Body.CopyToAsync(copy);
            seen = copy.Length;
        });

        await middleware.InvokeAsync(context);

        seen.ShouldBe(MCP_LIMIT);
    }

    private static string AddQuote(string deck, string quote)
        => """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"add_slide","arguments":{"deck":"DECK","layout":"quote","slots":{"quote":"QUOTE","attribution":"Sam"}}}}"""
            .Replace("DECK", deck, StringComparison.Ordinal).Replace("QUOTE", quote, StringComparison.Ordinal);

    // A valid sharing body padded with trailing spaces to exactly this many bytes.
    private static string SharingBody(int bytes)
    {
        const string Body = """{"level":"link","viewers":[]}""";
        return Body + new string(' ', bytes - Body.Length);
    }

    private async Task<HttpResponseMessage> PutSharingAsync(string deck, string body, bool chunked)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/decks/{deck}/sharing") { Content = ContentOf(body, chunked) };
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostMcpAsync(McpTestClient agent, string body, bool chunked)
    {
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = ContentOf(body, chunked) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.Tokens.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await http.SendAsync(request);
    }

    private static HttpContent ContentOf(string body, bool chunked)
    {
        HttpContent content = chunked ? new UnsizedContent(Encoding.UTF8.GetBytes(body)) : new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }
}
