using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// Every tool call's arguments, checked against the tool's own advertised inputSchema before the SDK binds them. A missing,
// null or wrongly typed argument gets a tool error that names it ("layout: required", "notes[0].slide: must be a whole
// number"), the K-style text the services use, not the SDK's generic "An error occurred invoking 'add_slide'.". That
// lets the tools keep strict, typed schemas. An argument or entry field the schema doesn't name is refused too
// ("viewer: unknown argument", "notes[0].x: unknown field"), wherever the schema says additionalProperties: false, which
// McpToolSchemas puts on every object schema that lists its properties. McpSetup registers both halves:
// - Unreadable, an incoming message filter. The SDK converts a request's params to its typed params before any request
//   filter runs, and an escaped lone surrogate ("\ud83d") can't become a .NET string, nor can an arguments that isn't an
//   object become a dictionary, so that conversion throws and the agent gets JSON-RPC -32603 "An error occurred.", which
//   names nothing. For a tools/call this filter finds the deepest path holding the bad text first ("notes[2].text",
//   "_meta.progressToken", "name"), makes that part convertible (an empty arguments, the field dropped) and leaves the
//   refusal in the message's Items for Filter. Any other request (tools/list's cursor, ping's params) gets -32602 Invalid
//   params naming the path instead, since there is no tool to give an error.
// - Filter, a call-tool filter. A call to a tool that doesn't exist goes on to the SDK's unknown-tool error, whatever
//   else is wrong with it, unless its name is the bad text. Otherwise it returns Unreadable's refusal (an argument the
//   tool doesn't name is "unknown" even when its value is also bad), or the first argument the schema refuses. A binding failure
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

    private const string NOT_AN_OBJECT = "arguments: must be an object";

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
            if (context.JsonRpcMessage is JsonRpcRequest { Params: JsonObject parameters } request)
            {
                if (request.Method == RequestMethods.ToolsCall)
                {
                    if (UnreadableToolCall(parameters) is { } unreadable)
                    {
                        context.Items[UNREADABLE_KEY] = unreadable;
                    }
                }
                else if (UnreadablePath(parameters, "params", top: true) is { } path)
                {
                    throw new McpProtocolException($"{path}: {UNREADABLE_TEXT}", McpErrorCode.InvalidParams);
                }
            }
            return next(context, cancellationToken);
        };

    // What a tools/call's params hold that the SDK can't convert, made convertible: the name (always refused, since no
    // tool's name holds it), an arguments that isn't an object, a lone surrogate in the arguments or in any other params
    // field such as _meta. The part is replaced or dropped, and the first refusal is returned for Filter. A name in params
    // that can't be read leaves no field of params readable, so that is -32602 here.
    // Over HTTP the transport gets there first: it reads params._meta for the protocol version before any filter runs,
    // so a name in params or in _meta that is itself the bad text is still -32603 from the SDK.
    private static UnreadableCall? UnreadableToolCall(JsonObject parameters)
    {
        List<string> fields;
        try
        {
            fields = parameters.Select(field => field.Key).ToList();
        }
        catch (InvalidOperationException)
        {
            throw new McpProtocolException($"params: {UNREADABLE_TEXT}", McpErrorCode.InvalidParams);
        }
        var name = fields.Contains("name") ? UnreadableName(parameters) : null;
        UnreadableCall? first = null;
        foreach (var field in fields.Where(field => field != "name"))
        {
            var found = field == "arguments" ? UnreadableArguments(parameters) : UnreadableField(parameters, field);
            first ??= found;
        }
        return name ?? first;
    }

    private static UnreadableCall? UnreadableName(JsonObject parameters)
    {
        if (Readable(parameters["name"]))
        {
            return null;
        }
        parameters["name"] = "";
        return new UnreadableCall($"name: {UNREADABLE_TEXT}", Argument: null, AnyTool: true);
    }

    // A tool's arguments must be an object: a client that double-encodes them sends a string.
    private static UnreadableCall? UnreadableArguments(JsonObject parameters)
    {
        if (parameters["arguments"] is null)
        {
            return null;
        }
        if (parameters["arguments"] is not JsonObject arguments)
        {
            parameters["arguments"] = new JsonObject();
            return new UnreadableCall(NOT_AN_OBJECT, Argument: null, AnyTool: false);
        }
        List<KeyValuePair<string, JsonNode?>> named;
        try
        {
            named = arguments.ToList();
        }
        catch (InvalidOperationException)
        {
            parameters["arguments"] = new JsonObject();
            return new UnreadableCall($"arguments: {UNREADABLE_TEXT}", Argument: null, AnyTool: false);
        }
        foreach (var (argument, value) in named)
        {
            if (UnreadablePath(value, argument) is { } path)
            {
                parameters["arguments"] = new JsonObject();
                return new UnreadableCall($"{path}: {UNREADABLE_TEXT}", argument, AnyTool: false);
            }
        }
        return null;
    }

    // Any other params field, such as _meta: dropped when it can't be read.
    private static UnreadableCall? UnreadableField(JsonObject parameters, string field)
    {
        if (UnreadablePath(parameters[field], field) is not { } path)
        {
            return null;
        }
        parameters.Remove(field);
        return new UnreadableCall($"{path}: {UNREADABLE_TEXT}", Argument: null, AnyTool: false);
    }

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (context, cancellationToken) =>
        {
            if (Refusal(context) is { } refusal)
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

    // Only a call to a tool that exists is refused here, apart from a name that can't be read: a call to an unknown tool
    // goes on to the SDK's unknown-tool error, whatever else is wrong with it. An argument the tool doesn't name is
    // refused as unknown even when its value is also unreadable.
    private static string? Refusal(RequestContext<CallToolRequestParams> context)
    {
        var unreadable = context.Items.TryGetValue(UNREADABLE_KEY, out var stored) ? stored as UnreadableCall : null;
        if (unreadable is { AnyTool: true })
        {
            return unreadable.Text;
        }
        if (context.MatchedPrimitive is not McpServerTool tool)
        {
            return null;
        }
        var schema = tool.ProtocolTool.InputSchema;
        if (unreadable != null)
        {
            return unreadable.Argument is { } name && Property(schema, name) is null && Closed(schema) ? $"{name}: unknown argument" : unreadable.Text;
        }
        return Check(schema, context.Params?.Arguments);
    }

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

    // A tools/call the SDK couldn't have read, as Unreadable left it for Filter: the refusal's text, the top-level argument
    // it is under (when it is in one), and whether it is refused whatever the tool (only an unreadable name is).
    private sealed record UnreadableCall(string Text, string? Argument, bool AnyTool);
}
