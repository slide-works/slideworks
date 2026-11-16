using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// Every tool call's arguments, checked against the tool's own advertised inputSchema before the SDK binds them. A missing,
// null or wrongly typed argument gets a tool error that names it ("layout: required", "notes[0].slide: must be a whole
// number"), the K-style text the services use, not the SDK's generic "An error occurred invoking 'add_slide'.". That
// lets the tools keep strict, typed schemas. An argument or entry field the schema doesn't name is refused too
// ("viewer: unknown argument", "notes[0].x: unknown field"), wherever the schema says additionalProperties: false, which
// McpToolSchemas puts on every object schema that lists its properties. McpSetup registers both halves:
// - Unreadable, an incoming message filter. The SDK converts a tools/call's params to CallToolRequestParams before any
//   call-tool filter runs, and an escaped lone surrogate ("\ud83d") can't become a .NET string, so that conversion throws
//   and the agent gets JSON-RPC -32603. This filter finds the deepest path holding it first ("notes[2].text"), empties the
//   arguments so the conversion succeeds, and leaves the refusal in the message's Items for Filter.
// - Filter, a call-tool filter. It returns that refusal, or the first argument the schema refuses. A binding failure
//   the schema can't predict, such as a whole number too big for an int, is still a tool error. Only a binding failure:
//   McpToolErrors.Run calls ArgumentsBound as the tool body starts, and an exception after that is a bug, rethrown for
//   the SDK to log ("threw an unhandled exception") and report as one, never mistaken for an argument error after a
//   write may have committed.
// The services keep their own checks: they cover what the schema leaves open (an explicit null in a nullable list, the
// slots inside a layout) and callers that don't come through MCP.
public static class McpArgumentCheck
{
    public const string BINDING_FAILED = "arguments: don't fit the tool's parameters";

    private const string UNREADABLE_KEY = "slideworks.mcp.unreadable-argument";

    private const string UNREADABLE_TEXT = "text is not valid UTF-16";

    // Set by Filter around each call; McpToolErrors.Run flips it once the SDK has bound the arguments.
    private static readonly AsyncLocal<StrongBox<bool>?> BOUND = new();

    // The tool body has started: the SDK bound the arguments. Every tool calls this through McpToolErrors.Run.
    public static void ArgumentsBound()
    {
        if (BOUND.Value is { } bound)
        {
            bound.Value = true;
        }
    }

    public static McpMessageHandler Unreadable(McpMessageHandler next)
        => (context, cancellationToken) =>
        {
            if (context.JsonRpcMessage is JsonRpcRequest { Method: RequestMethods.ToolsCall, Params: JsonObject call }
                && call["arguments"] is JsonObject arguments
                && UnreadablePath(arguments, "arguments", top: true) is { } path)
            {
                context.Items[UNREADABLE_KEY] = $"{path}: {UNREADABLE_TEXT}";
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
            var bound = new StrongBox<bool>();
            BOUND.Value = bound;
            try
            {
                return await next(context, cancellationToken);
            }
            catch (Exception error) when (!bound.Value && error is JsonException or ArgumentException { ParamName: "arguments" })
            {
                // The SDK's binder, before the tool body started: a JsonException for a value it can't convert, an
                // ArgumentException naming "arguments" for a missing parameter.
                return McpToolErrors.Refusal(BINDING_FAILED);
            }
        };

    // The deepest path whose JSON holds text that isn't valid UTF-16 (reading a name or writing out a value unescapes
    // it, and that is what throws): "notes[2].text" for a value, the object's own path ("notes[0]", or "arguments" at the
    // top) when one of its names is the bad text, or null when all of it reads.
    private static string? UnreadablePath(JsonNode? node, string path, bool top = false)
    {
        switch (node)
        {
            case JsonObject properties:
                try
                {
                    foreach (var (name, value) in properties)
                    {
                        if (UnreadablePath(value, top ? name : $"{path}.{name}") is { } found)
                        {
                            return found;
                        }
                    }
                    return null;
                }
                catch (InvalidOperationException)
                {
                    return path;
                }
            case JsonArray entries:
                return entries.Select((entry, index) => UnreadablePath(entry, $"{path}[{index}]")).FirstOrDefault(found => found != null);
            default:
                return Readable(node) ? null : path;
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
    // argument in the order sent: one the schema doesn't name is refused when the schema is closed (additionalProperties:
    // false), and one it describes is checked against that.
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
            if (CheckNamed(schema, name, name, value, "unknown argument") is { } error)
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
            if (CheckNamed(schema, property.Name, $"{path}.{property.Name}", property.Value, "unknown field") is { } error)
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

    // One named argument or field: checked against its schema, or refused as unknown when the schema is closed.
    private static string? CheckNamed(JsonElement schema, string name, string path, JsonElement value, string unknown)
    {
        if (Property(schema, name) is { } property)
        {
            return CheckValue(path, value, property);
        }
        return Closed(schema) ? $"{path}: {unknown}" : null;
    }

    private static bool Closed(JsonElement schema)
        => schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False;

    private static bool AllowsNull(JsonElement? schema) => schema is { } given && Types(given).Contains("null");
}
