using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// Every MCP tool runs its work through Run (K6). A result always goes back as JSON text. When that JSON is an object it also
// goes in structuredContent; any other kind (an array, a number, a string, null) goes as text only, because the MCP spec
// types structuredContent as an object and clients built on the TypeScript SDK refuse the whole result otherwise. So a
// tool that lists things returns a wrapper object, such as { decks: [...] }, to get structured content. A BadRequestException, NotFoundException or ConflictException goes back as a tool result with
// isError: true and the same message the API would give. Anything else is a bug, and the SDK reports it as one.
public static class McpToolErrors
{
    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<CallToolResult> Run<T>(Func<Task<T>> work)
    {
        try
        {
            var json = JsonSerializer.SerializeToElement(await work(), JSON);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = json.GetRawText() }],
                StructuredContent = json.ValueKind == JsonValueKind.Object ? json : null,
            };
        }
        catch (Exception error) when (error is BadRequestException or NotFoundException or ConflictException)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = error.Message }],
            };
        }
    }
}
