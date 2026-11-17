using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SlideWorks.Core.Look.Manifest;

// Reads and writes a template manifest (T3), the format of src/contracts/template-manifest.schema.json. Parse refuses
// an unknown field and checks the two value rules a custom layout's CSS relies on: every palette value is # plus 6 hex
// digits, and shape.shadow passes ManifestShadow. Counts and sizes (TemplateLimits) are checked where files are stored.
public static partial class ManifestJson
{
    public const int MAX_NAME = 40;
    public const int MAX_FILE = 200;

    private static readonly string[] Root =
    [
        "name", "label", "purpose", "basedOn", "layouts", "chrome", "outline", "houseStyle", "type", "shape", "fonts",
        "logos", "titleArt", "ornament", "palette", "lockPalette", "customLayouts",
    ];

    private static readonly string[] Footers = ["author", "date", "off"];
    private static readonly string[] TopBars = ["none", "accent"];
    private static readonly string[] Roles = ["head", "body", "mono"];
    private static readonly string[] Styles = ["normal", "italic"];
    private static readonly string[] ArtKinds = ["none", "generator", "image"];
    private static readonly string[] ExtraNames = ["extra-1", "extra-2", "extra-3", "extra-4"];

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static ManifestParseResult Parse(string json)
    {
        var reader = new ManifestReader();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new ManifestParseResult { Errors = [new ManifestError("", "is not valid JSON")] };
        }

        using (document)
        {
            var manifest = ReadManifest(reader, document.RootElement);
            return reader.Errors.Count == 0
                ? new ManifestParseResult { Manifest = manifest, Errors = [] }
                : new ManifestParseResult { Errors = reader.Errors };
        }
    }

    // The manifest as template.json: two-space indent, fields in the schema's order, unset optional fields left out.
    public static string Write(TemplateManifest manifest)
    {
        var root = new JsonObject
        {
            ["name"] = manifest.Name,
            ["label"] = manifest.Label,
            ["purpose"] = manifest.Purpose,
            ["basedOn"] = manifest.BasedOn,
            ["layouts"] = List(manifest.Layouts),
            ["chrome"] = new JsonObject
            {
                ["tracker"] = manifest.Chrome.Tracker,
                ["footer"] = manifest.Chrome.Footer,
                ["progress"] = manifest.Chrome.Progress,
                ["deepDivesOnDivider"] = manifest.Chrome.DeepDivesOnDivider,
                ["draft"] = manifest.Chrome.Draft,
                ["topBar"] = manifest.Chrome.TopBar,
            },
            ["outline"] = List(manifest.Outline),
            ["houseStyle"] = List(manifest.HouseStyle),
            ["type"] = new JsonObject
            {
                ["head"] = manifest.Type.Head,
                ["body"] = manifest.Type.Body,
                ["headWeight"] = manifest.Type.HeadWeight,
                ["headTracking"] = manifest.Type.HeadTracking,
            },
            ["shape"] = new JsonObject
            {
                ["radius"] = new JsonObject
                {
                    ["sm"] = manifest.Shape.Radius.Sm,
                    ["md"] = manifest.Shape.Radius.Md,
                    ["lg"] = manifest.Shape.Radius.Lg,
                    ["xl"] = manifest.Shape.Radius.Xl,
                },
                ["shadow"] = manifest.Shape.Shadow,
            },
            ["fonts"] = new JsonArray(manifest.Fonts.Select(font => (JsonNode)new JsonObject
            {
                ["role"] = font.Role,
                ["family"] = font.Family,
                ["file"] = font.File,
                ["weight"] = font.Weight,
                ["style"] = font.Style,
            }).ToArray()),
        };

        var logos = new JsonObject();
        AddIfSet(logos, "light", manifest.Logos.Light);
        AddIfSet(logos, "dark", manifest.Logos.Dark);
        logos["showOnDark"] = manifest.Logos.ShowOnDark;
        root["logos"] = logos;

        var art = new JsonObject { ["kind"] = manifest.TitleArt.Kind };
        AddIfSet(art, "generator", manifest.TitleArt.Generator);
        AddIfSet(art, "file", manifest.TitleArt.File);
        root["titleArt"] = art;

        root["ornament"] = new JsonObject { ["dots"] = List(manifest.Ornament.Dots) };
        if (manifest.Palette is { } palette)
        {
            root["palette"] = WritePalette(palette);
        }

        root["lockPalette"] = manifest.LockPalette;
        root["customLayouts"] = new JsonArray(manifest.CustomLayouts.Select(layout => (JsonNode)new JsonObject
        {
            ["name"] = layout.Name,
            ["file"] = layout.File,
        }).ToArray());

        return root.ToJsonString(Indented) + "\n";
    }

    private static JsonObject WritePalette(ManifestPalette palette)
    {
        var node = new JsonObject();
        foreach (var key in ManifestPalette.Keys)
        {
            node[key] = palette.Tokens[key];
        }

        if (palette.Extras.Count > 0)
        {
            node["extras"] = new JsonArray(palette.Extras.Select(extra => (JsonNode)new JsonObject
            {
                ["name"] = extra.Name,
                ["colour"] = extra.Colour,
                ["ink"] = extra.Ink,
            }).ToArray());
        }

        return node;
    }

    private static JsonArray List(IReadOnlyList<string> values) => new(values.Select(value => (JsonNode)JsonValue.Create(value)).ToArray());

    private static void AddIfSet(JsonObject node, string key, string? value)
    {
        if (value is not null)
        {
            node[key] = value;
        }
    }

    private static TemplateManifest ReadManifest(ManifestReader outer, JsonElement root)
    {
        // A root that is not an object is one error; the rest is then read from an empty object into a reader nobody keeps.
        var reader = outer.IsObject(root, "", Root) ? outer : new ManifestReader();
        root = ReferenceEquals(reader, outer) ? root : Empty;
        var name = reader.Text(root, "", "name");
        if (root.TryGetProperty("name", out var given) && given.ValueKind == JsonValueKind.String && !ShortName().IsMatch(name))
        {
            reader.Add("name", $"must be 1 to {MAX_NAME} characters: lower-case letters, digits and hyphens");
        }

        return ReadFields(reader, root, name);
    }

    private static TemplateManifest ReadFields(ManifestReader reader, JsonElement root, string name) => new()
    {
        Name = name,
        Label = reader.Text(root, "", "label", minLength: 1),
        Purpose = reader.Text(root, "", "purpose"),
        BasedOn = reader.Text(root, "", "basedOn", minLength: 1),
        Layouts = reader.Texts(root, "", "layouts", minLength: 1),
        Chrome = ReadChrome(reader, root),
        Outline = reader.Texts(root, "", "outline"),
        HouseStyle = reader.Texts(root, "", "houseStyle"),
        Type = ReadType(reader, root),
        Shape = ReadShape(reader, root),
        Fonts = reader.Items(root, "", "fonts").Select(entry => ReadFont(reader, entry.Item, entry.Path)).ToList(),
        Logos = ReadLogos(reader, root),
        TitleArt = ReadTitleArt(reader, root),
        Ornament = ReadOrnament(reader, root),
        Palette = ReadPalette(reader, root),
        LockPalette = reader.Flag(root, "", "lockPalette"),
        CustomLayouts = reader.Items(root, "", "customLayouts").Select(entry => ReadLayout(reader, entry.Item, entry.Path)).ToList(),
    };

    // The field as an object whose fields are all known; otherwise an empty object whose reads record nothing.
    private static (JsonElement Element, ManifestReader Reader) Section(ManifestReader reader, JsonElement parent, string key, string[] known, bool required = true)
    {
        if (reader.Field(parent, "", key, required) is { } value && reader.IsObject(value, key, known))
        {
            return (value, reader);
        }

        return (Empty, new ManifestReader());
    }

    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement.Clone();

    private static ManifestChrome ReadChrome(ManifestReader outer, JsonElement root)
    {
        var (chrome, reader) = Section(outer, root, "chrome", ["tracker", "footer", "progress", "deepDivesOnDivider", "draft", "topBar"]);
        return new ManifestChrome
        {
            Tracker = reader.Flag(chrome, "chrome", "tracker"),
            Footer = reader.OneOf(chrome, "chrome", "footer", Footers),
            Progress = reader.Flag(chrome, "chrome", "progress"),
            DeepDivesOnDivider = reader.Flag(chrome, "chrome", "deepDivesOnDivider"),
            Draft = reader.Flag(chrome, "chrome", "draft"),
            TopBar = reader.OneOf(chrome, "chrome", "topBar", TopBars),
        };
    }

    private static ManifestType ReadType(ManifestReader outer, JsonElement root)
    {
        var (type, reader) = Section(outer, root, "type", ["head", "body", "headWeight", "headTracking"]);
        return new ManifestType
        {
            Head = reader.Text(type, "type", "head", minLength: 1),
            Body = reader.Text(type, "type", "body", minLength: 1),
            HeadWeight = reader.Whole(type, "type", "headWeight", 100, 900),
            HeadTracking = reader.Text(type, "type", "headTracking"),
        };
    }

    private static ManifestShape ReadShape(ManifestReader outer, JsonElement root)
    {
        var (shape, reader) = Section(outer, root, "shape", ["radius", "shadow"]);
        var radius = Empty;
        var radiusReader = new ManifestReader();
        if (reader.Field(shape, "shape", "radius") is { } value && reader.IsObject(value, "shape.radius", ["sm", "md", "lg", "xl"]))
        {
            (radius, radiusReader) = (value, reader);
        }

        var shadow = reader.Text(shape, "shape", "shadow");
        if (shape.TryGetProperty("shadow", out var given) && given.ValueKind == JsonValueKind.String && !ManifestShadow.IsSafe(shadow))
        {
            reader.Add("shape.shadow", "must be none, or shadows made only of px lengths and one rgb() colour each, such as 0 1px 2px rgb(15 23 42 / 0.06)");
        }

        return new ManifestShape
        {
            Radius = new ManifestRadius
            {
                Sm = radiusReader.Whole(radius, "shape.radius", "sm", 0, int.MaxValue),
                Md = radiusReader.Whole(radius, "shape.radius", "md", 0, int.MaxValue),
                Lg = radiusReader.Whole(radius, "shape.radius", "lg", 0, int.MaxValue),
                Xl = radiusReader.Whole(radius, "shape.radius", "xl", 0, int.MaxValue),
            },
            Shadow = shadow,
        };
    }

    private static ManifestFonts ReadFont(ManifestReader outer, JsonElement font, string path)
    {
        var reader = outer.IsObject(font, path, ["role", "family", "file", "weight", "style"]) ? outer : new ManifestReader();
        font = ReferenceEquals(reader, outer) ? font : Empty;
        return new ManifestFonts
        {
            Role = reader.OneOf(font, path, "role", Roles),
            Family = reader.Text(font, path, "family", minLength: 1),
            File = reader.Text(font, path, "file", minLength: 1, maxLength: MAX_FILE),
            Weight = reader.Whole(font, path, "weight", 100, 900),
            Style = reader.OneOf(font, path, "style", Styles),
        };
    }

    private static ManifestLogos ReadLogos(ManifestReader outer, JsonElement root)
    {
        var (logos, reader) = Section(outer, root, "logos", ["light", "dark", "showOnDark"]);
        return new ManifestLogos
        {
            Light = reader.OptionalText(logos, "logos", "light", minLength: 1, maxLength: MAX_FILE),
            Dark = reader.OptionalText(logos, "logos", "dark", minLength: 1, maxLength: MAX_FILE),
            ShowOnDark = reader.Flag(logos, "logos", "showOnDark"),
        };
    }

    private static ManifestTitleArt ReadTitleArt(ManifestReader outer, JsonElement root)
    {
        var (art, reader) = Section(outer, root, "titleArt", ["kind", "generator", "file"]);
        return new ManifestTitleArt
        {
            Kind = reader.OneOf(art, "titleArt", "kind", ArtKinds),
            Generator = reader.OptionalText(art, "titleArt", "generator", minLength: 1),
            File = reader.OptionalText(art, "titleArt", "file", minLength: 1, maxLength: MAX_FILE),
        };
    }

    private static ManifestOrnament ReadOrnament(ManifestReader outer, JsonElement root)
    {
        var (ornament, reader) = Section(outer, root, "ornament", ["dots"]);
        return new ManifestOrnament { Dots = reader.Texts(ornament, "ornament", "dots", minLength: 1) };
    }

    private static ManifestPalette? ReadPalette(ManifestReader outer, JsonElement root)
    {
        if (!root.TryGetProperty("palette", out _))
        {
            return null;
        }

        var (palette, reader) = Section(outer, root, "palette", [.. ManifestPalette.Keys, "extras"]);
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in ManifestPalette.Keys)
        {
            tokens[key] = Colour(reader, palette, "palette", key);
        }

        var extras = reader.Items(palette, "palette", "extras", required: false).Select(entry =>
        {
            var inner = reader.IsObject(entry.Item, entry.Path, ["name", "colour", "ink"]) ? reader : new ManifestReader();
            var extra = ReferenceEquals(inner, reader) ? entry.Item : Empty;
            return new ManifestExtra
            {
                Name = inner.OneOf(extra, entry.Path, "name", ExtraNames),
                Colour = Colour(inner, extra, entry.Path, "colour"),
                Ink = Colour(inner, extra, entry.Path, "ink"),
            };
        }).ToList();

        return new ManifestPalette { Tokens = tokens, Extras = extras };
    }

    private static string Colour(ManifestReader reader, JsonElement parent, string path, string key)
    {
        var colour = reader.Text(parent, path, key);
        if (parent.TryGetProperty(key, out var given) && given.ValueKind == JsonValueKind.String && !Hex().IsMatch(colour))
        {
            reader.Add(ManifestReader.Join(path, key), "must be # and 6 hex digits, such as #2f5bea");
        }

        return colour;
    }

    private static ManifestCustomLayout ReadLayout(ManifestReader outer, JsonElement layout, string path)
    {
        var reader = outer.IsObject(layout, path, ["name", "file"]) ? outer : new ManifestReader();
        layout = ReferenceEquals(reader, outer) ? layout : Empty;
        return new ManifestCustomLayout
        {
            Name = reader.Text(layout, path, "name", minLength: 1),
            File = reader.Text(layout, path, "file", minLength: 1, maxLength: MAX_FILE),
        };
    }

    [GeneratedRegex(@"\A[a-z0-9-]{1,40}\z")]
    private static partial Regex ShortName();

    [GeneratedRegex(@"\A#[0-9a-fA-F]{6}\z")]
    private static partial Regex Hex();
}
