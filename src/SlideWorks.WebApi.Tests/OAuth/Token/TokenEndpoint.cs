using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// The agent's side of POST /oauth/token and /oauth/revoke on the wire, plus the seams the token tests start from: a code
// issued through IOAuthCodes (the K10 contract seam, so these tests never lean on the authorize endpoints), and a raw MCP
// initialize to see whether /mcp takes a token.
public sealed class TokenEndpoint(WebApplicationFactory<ApiMarker> factory, HttpClient anonymous)
{
    // A code for the presenter (Sam unless named), bound to the client, OAuthFlow's redirect and challenge, and this
    // server's /mcp.
    public async Task<string> CodeAsync(string clientId, TestUser? presenter = null)
    {
        using var scope = factory.Services.CreateScope();
        var person = await TestPeople.SignInAsync(scope.ServiceProvider, presenter ?? TestUser.Sam);
        return await scope.ServiceProvider.GetRequiredService<IOAuthCodes>()
            .IssueAsync(clientId, person.Id, OAuthFlow.REDIRECT, OAuthFlow.CHALLENGE, "decks", OAuthFlow.Resource);
    }

    // grant_type=authorization_code with every parameter right for the code; each override replaces one, and a null value
    // drops it.
    public Task<HttpResponseMessage> RedeemAsync(string clientId, string code, params (string Name, string? Value)[] overrides)
        => PostAsync("/oauth/token", Form(
            [
                ("grant_type", "authorization_code"),
                ("code", code),
                ("client_id", clientId),
                ("redirect_uri", OAuthFlow.REDIRECT),
                ("code_verifier", OAuthFlow.VERIFIER),
                ("resource", OAuthFlow.Resource),
            ],
            overrides));

    public Task<HttpResponseMessage> RefreshAsync(string clientId, string refreshToken)
        => PostAsync("/oauth/token", Form([("grant_type", "refresh_token"), ("refresh_token", refreshToken), ("client_id", clientId)], []));

    public Task<HttpResponseMessage> RevokeAsync(string token)
        => PostAsync("/oauth/revoke", Form([("token", token)], []));

    // A code redeemed into tokens over the wire: (access_token, refresh_token).
    public async Task<(string Access, string Refresh)> TokensAsync(string clientId, TestUser? presenter = null)
    {
        var body = await OkBodyAsync(await RedeemAsync(clientId, await CodeAsync(clientId, presenter)));
        return (body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!);
    }

    // The status of an MCP initialize with this bearer token: 200 when /mcp takes it, 401 when it doesn't.
    public async Task<HttpStatusCode> McpStatusAsync(string accessToken)
    {
        using var http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request);
        return response.StatusCode;
    }

    public static async Task<JsonElement> OkBodyAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // The RFC 6749 error code of a refused request, after checking its status.
    public static async Task<string> ErrorAsync(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        var body = JsonDocument.Parse(text).RootElement;
        body.GetProperty("error_description").GetString().ShouldNotBeNullOrEmpty();
        return body.GetProperty("error").GetString()!;
    }

    private Task<HttpResponseMessage> PostAsync(string path, FormUrlEncodedContent form) => anonymous.PostAsync(path, form);

    private static FormUrlEncodedContent Form((string Name, string? Value)[] fields, (string Name, string? Value)[] overrides)
    {
        var form = fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);
        foreach (var (name, value) in overrides)
        {
            form[name] = value;
        }
        return new FormUrlEncodedContent(form.Where(f => f.Value != null).Select(f => KeyValuePair.Create(f.Key, f.Value!)));
    }
}
