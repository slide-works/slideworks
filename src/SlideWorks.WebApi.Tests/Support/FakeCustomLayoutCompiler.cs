using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates.Layouts;

namespace SlideWorks.WebApi.Tests.Support;

// ICustomLayoutCompiler for tests that need a compiled layout while Core's stages refuse everything. Compile answers
// what Accept or Refuse set last (it refuses with custom_layouts_unavailable until a test sets one) and keeps the scope
// it was given in LastScope; a scope that is not 8 lower-case hex characters throws, as the real one does. Read is the
// real CompiledLayoutJson.Read, every check included, over a stand-in for the slot schema compiler: it reads the stored
// slots.json as written, without checking it, and takes the definition from WithDefinition by the schema's name (hand
// it one from LayoutCatalogue.Find). A stored layout whose name has no definition handed in throws
// InvalidOperationException, as a schema the real compiler refuses does. Add it with FakeCustomLayoutCompiler.AddTo.
public sealed class FakeCustomLayoutCompiler : ICustomLayoutCompiler, ISlotSchemaCompiler
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, LayoutDefinition> _definitions = new(StringComparer.Ordinal);
    private CompileResult _next = Unavailable();

    // The scope Compile was last given, or null before the first call.
    public string? LastScope { get; private set; }

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeCustomLayoutCompiler>().AddSingleton<ICustomLayoutCompiler>(provider => provider.GetRequiredService<FakeCustomLayoutCompiler>());

    // Every Compile from now on returns this layout.
    public void Accept(CompiledLayout layout)
    {
        lock (_gate)
        {
            _next = new CompileResult(layout, []);
        }
    }

    // Every Compile from now on returns these errors and no layout.
    public void Refuse(params CustomLayoutError[] errors)
    {
        lock (_gate)
        {
            _next = new CompileResult(null, errors);
        }
    }

    // The definition Read gives a stored layout whose schema has this name.
    public FakeCustomLayoutCompiler WithDefinition(string name, LayoutDefinition definition)
    {
        lock (_gate)
        {
            _definitions[name] = definition;
        }

        return this;
    }

    public CompileResult Compile(string markup, string css, string slotsJson, string scope)
    {
        // Throws for a bad scope exactly as the pipeline does.
        _ = LayoutClassPrefix.Of(scope, LayoutScope.UNNAMED);
        lock (_gate)
        {
            LastScope = scope;
            return _next;
        }
    }

    public CompiledLayout Read(string storedJson) => CompiledLayoutJson.Read(storedJson, this);

    // The stand-in for stage 2: the schema as stored, and the definition handed in for its name.
    (LayoutDefinition? Definition, CustomLayoutSchema? Schema, IReadOnlyList<CustomLayoutError> Errors) ISlotSchemaCompiler.Compile(string slotsJson, int fixedSteps)
    {
        using var document = JsonDocument.Parse(slotsJson);
        var root = document.RootElement;
        var name = root.GetProperty("name").GetString()!;
        LayoutDefinition? definition;
        lock (_gate)
        {
            _definitions.TryGetValue(name, out definition);
        }

        if (definition is null)
        {
            return (null, null, Unavailable().Errors);
        }

        var schema = new CustomLayoutSchema(
            name,
            root.GetProperty("description").GetString()!,
            root.GetProperty("surface").GetString()!,
            root.TryGetProperty("steps", out var steps) ? steps.GetString() : null,
            Slots(root.GetProperty("slots")));
        return (definition, schema, []);
    }

    private static CompileResult Unavailable()
        => new(null, [CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE)]);

    private static IReadOnlyList<CustomSlot> Slots(JsonElement slots) => [.. slots.EnumerateObject().Select(slot => Slot(slot.Name, slot.Value))];

    private static CustomSlot Slot(string name, JsonElement json)
    {
        // A list of one scalar type writes the type itself under "of"; a list of objects writes its fields there.
        var of = json.TryGetProperty("of", out var items) ? items : (JsonElement?)null;
        var scalar = of is { } one && one.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String;
        return new CustomSlot(
            name,
            json.GetProperty("type").GetString()!,
            json.TryGetProperty("optional", out var optional) && optional.GetBoolean(),
            json.TryGetProperty("min", out var min) ? min.GetInt32() : null,
            json.TryGetProperty("max", out var max) ? max.GetInt32() : null,
            json.TryGetProperty("values", out var values) ? [.. values.EnumerateArray().Select(value => value.GetString()!)] : [],
            of is { } fields && !scalar ? Slots(fields) : [],
            of is { } item && scalar ? Slot(CustomSlot.ITEM, item) : null);
    }
}
