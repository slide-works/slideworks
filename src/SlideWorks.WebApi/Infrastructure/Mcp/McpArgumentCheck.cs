using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// Every tool call's arguments, checked against the tool's own advertised inputSchema before the SDK binds them. A missing,
// null or wrongly typed argument gets a tool error that names it ("layout: required", "notes[0].slide: must be a whole
// number"), the K-style text the services use, not the SDK's generic "An error occurred invoking 'add_slide'.". That
// lets the tools keep strict, typed schemas. McpSetup registers both halves:
// - Unreadable, an incoming message filter. The SDK converts a tools/call's params to CallToolRequestParams before any
//   call-tool filter runs, and an escaped lone surrogate ("\ud83d") can't become a .NET string, so that conversion throws
//   and the agent gets JSON-RPC -32603. This filter finds the argument first, empties the arguments so the conversion
//   succeeds, and leaves the refusal in the message's Items for Filter.
// - Filter, a call-tool filter. It returns that refusal, or the first argument the schema refuses. A binding failure
//   the schema can't predict, such as a whole number too big for an int, is still a tool error.
// The services keep their own checks: they cover what the schema leaves open (an explicit null in a nullable list, the
// slots inside a layout) and callers that don't come through MCP.
public static class McpArgumentCheck
{
    public const string BINDING_FAILED = "arguments: don't fit the tool's parameters";

    private const string UNREADABLE_KEY = "slideworks.mcp.unreadable-argument";

    private const string UNREADABLE_TEXT = "text is not valid UTF-16";

    public static McpMessageHandler Unreadable(McpMessageHandler next)
        => (context, cancellationToken) =>
        {
            if (context.JsonRpcMessage is JsonRpcRequest { Method: RequestMethods.ToolsCall, Params: JsonObject call }
                && call["arguments"] is JsonObject arguments
                && UnreadableArgument(arguments) is { } name)
            {
                context.Items[UNREADABLE_KEY] = $"{name}: {UNREADABLE_TEXT}";
                call["arguments"] = new JsonObject();
            }
            return next(context, cancellationToken);
        };

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (context, cancellationToken) =>
        {
            var refusal = context.Items.TryGetValue(UNREADABLE_KEY, out var unreadable) ? unreadable as string : Check(context);
            if (refusal != null)
            {
                return McpToolErrors.Refusal(refusal);
            }
            try
            {
                return await next(context, cancellationToken);
            }
            catch (Exception error) when (error is JsonException or ArgumentException { ParamName: "arguments" })
            {
                // The SDK's binder: a JsonException for a value it can't convert, an ArgumentException naming
                // "arguments" for a missing parameter. Tool bodies run inside McpToolErrors.Run and throw neither.
                return McpToolErrors.Refusal(BINDING_FAILED);
            }
        };

    // The first argument whose JSON holds text that isn't valid UTF-16 (writing it out unescapes every string in it, and
    // that is what throws), "arguments" when an argument's own name is the bad text, or null when all of it reads.
    private static string? UnreadableArgument(JsonObject arguments)
    {
        try
        {
            return arguments.FirstOrDefault(argument => !Readable(argument.Value)).Key;
        }
        catch (InvalidOperationException)
        {
            return "arguments";
        }
    }

    private static bool Readable(JsonNode? value)
    {
        try
        {
            _ = value?.ToJsonString();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string? Check(RequestContext<CallToolRequestParams> context)
        => context.MatchedPrimitive is McpServerTool tool ? Check(tool.ProtocolTool.InputSchema, context.Params?.Arguments) : null;

    // The tool's own schema: every required argument present and not null (unless its type allows null), then every
    // argument the schema describes, in the order sent. Arguments the schema doesn't name are left to the SDK, which
    // ignores them.
    private static string? Check(JsonElement schema, IDictionary<string, JsonElement>? arguments)
    {
        arguments ??= new Dictionary<string, JsonElement>();
        foreach (var name in Required(schema))
        {
            if (!arguments.TryGetValue(name, out var value) || (value.ValueKind == JsonValueKind.Null && !AllowsNull(Property(schema, name))))
            {
                return $"{name}: required";
            }
        }
        foreach (var (name, value) in arguments)
        {
            if (Property(schema, name) is { } property && CheckValue(name, value, property) is { } error)
            {
                return error;
            }
        }
        return null;
    }

    private static string? CheckValue(string path, JsonElement value, JsonElement schema)
    {
        var types = Types(schema);
        if (types.Count > 0 && !types.Any(type => Is(value, type)))
        {
            return $"{path}: must be {Phrase(schema, plural: false, inheritedFormat: null)}";
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            return CheckObject(path, value, schema);
        }
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            return CheckItems(path, value, schema, items);
        }
        return null;
    }

    private static string? CheckObject(string path, JsonElement value, JsonElement schema)
    {
        foreach (var name in Required(schema))
        {
            if (!value.TryGetProperty(name, out var given) || (given.ValueKind == JsonValueKind.Null && !AllowsNull(Property(schema, name))))
            {
                return $"{path}.{name}: required";
            }
        }
        foreach (var property in value.EnumerateObject())
        {
            if (Property(schema, property.Name) is { } propertySchema && CheckValue($"{path}.{property.Name}", property.Value, propertySchema) is { } error)
            {
                return error;
            }
        }
        return null;
    }

    // A list of objects or lists names the entry ("notes[1]: must be an object with slide and text"); a list of plain
    // values names the list ("viewers: must be a list of email addresses").
    private static string? CheckItems(string path, JsonElement value, JsonElement schema, JsonElement items)
    {
        var itemTypes = Types(items);
        if (itemTypes.Any(type => type is "object" or "array"))
        {
            return value.EnumerateArray().Select((item, index) => CheckValue($"{path}[{index}]", item, items)).FirstOrDefault(error => error != null);
        }
        return itemTypes.Count > 0 && value.EnumerateArray().Any(item => !itemTypes.Any(type => Is(item, type)))
            ? $"{path}: must be {Phrase(schema, plural: false, inheritedFormat: null)}"
            : null;
    }

    private static bool Is(JsonElement value, string type) => type switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => true,
    };

    // What the schema's type reads as: "text", "a whole number", "a list of email addresses", "an object with slide and
    // text". A list's format (where a [EmailAddress] on the parameter puts it) describes its entries.
    private static string Phrase(JsonElement schema, bool plural, string? inheritedFormat)
    {
        var format = schema.TryGetProperty("format", out var given) && given.ValueKind == JsonValueKind.String ? given.GetString() : inheritedFormat;
        var names = Types(schema).Where(type => type != "null").Select(type => type switch
        {
            "string" when format == "email" => plural ? "email addresses" : "an email address",
            "string" => "text",
            "integer" => plural ? "whole numbers" : "a whole number",
            "number" => plural ? "numbers" : "a number",
            "boolean" => plural ? "true or false values" : "true or false",
            "object" => (plural ? "objects" : "an object") + With(Required(schema)),
            "array" => (plural ? "lists" : "a list") + (schema.TryGetProperty("items", out var items) ? $" of {Phrase(items, plural: true, format)}" : ""),
            _ => type,
        }).ToList();
        if (names.Count == 0)
        {
            return plural ? "values" : "a value";
        }
        return string.Join(" or ", names);
    }

    private static string With(IReadOnlyList<string> required) => required.Count switch
    {
        0 => "",
        1 => $" with {required[0]}",
        _ => $" with {string.Join(", ", required.Take(required.Count - 1))} and {required[^1]}",
    };

    private static IReadOnlyList<string> Types(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return [];
        }
        return type.ValueKind switch
        {
            JsonValueKind.String => [type.GetString()!],
            JsonValueKind.Array => type.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()!).ToList(),
            _ => [],
        };
    }

    private static IReadOnlyList<string> Required(JsonElement schema)
        => schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array
            ? required.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.String).Select(r => r.GetString()!).ToList()
            : [];

    private static JsonElement? Property(JsonElement schema, string name)
        => schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(name, out var property)
            ? property
            : null;

    private static bool AllowsNull(JsonElement? schema) => schema is { } given && Types(given).Contains("null");
}
