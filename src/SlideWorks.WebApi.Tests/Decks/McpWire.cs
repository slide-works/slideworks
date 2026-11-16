using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SlideWorks.WebApi.Tests.Decks;

// A tools/call POSTed straight to /mcp, so a test sees the JSON-RPC result exactly as it came over the wire, not the C#
// client's view of it (clients built on the TypeScript SDK refuse a result whose structuredContent is not an object),
// and can send JSON the C# client would never write, such as an escaped lone surrogate.
public static class McpWire
{
    // The JSON-RPC result of a tools/call that the server answered with a result (a tool error is a result too).
    public static async Task<JsonElement> CallToolAsync(WebApplicationFactory<ApiMarker> factory, string accessToken, string tool, string argumentsJson)
    {
        var message = await SendToolCallAsync(factory, accessToken, tool, argumentsJson);
        message.TryGetProperty("result", out var result).ShouldBeTrue(message.GetRawText());
        return result;
    }

    // The whole JSON-RPC response message, for a call the server may answer with a JSON-RPC error instead of a result.
    public static async Task<JsonElement> SendToolCallAsync(WebApplicationFactory<ApiMarker> factory, string accessToken, string tool, string argumentsJson)
    {
        using var http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $$$"""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{argumentsJson}}}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        // A streamable HTTP response is either one JSON body or an event stream whose data line holds the message.
        var message = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
            ? body.Split('\n').Select(line => line.TrimEnd('\r')).Last(line => line.StartsWith("data:", StringComparison.Ordinal))["data:".Length..]
            : body;
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        root.GetProperty("id").GetInt32().ShouldBe(7);
        return root.Clone();
    }

    public static string TextOf(JsonElement result)
        => result.GetProperty("content").EnumerateArray().Single(block => block.GetProperty("type").GetString() == "text").GetProperty("text").GetString()!;
}
