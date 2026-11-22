using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// src/contracts/layout-catalogue.json is generated from LayoutCatalogue: every layout's name, surface, tracker and
// top-level slots with their kinds and whether each is required. The client's literal mirrors (slots.ts,
// layoutSlots.ts) are checked against it by src/SlideWorks.Client/src/slides/layoutCatalogueMirror.test.ts.
[TestFixture]
public class LayoutCatalogueJsonTests
{
    private const string FILE = "layout-catalogue.json";

    private const string REGENERATE =
        "UPDATE_LAYOUTS_DOC=1 tools/agent/test-dotnet src/SlideWorks.UnitTest \"FullyQualifiedName~LayoutCatalogueJsonTests\"";

    private static readonly JsonSerializerOptions _indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Test]
    public void Json_WhenCatalogueChanges_ShouldMatchTheGeneratedFile()
    {
        var expected = Render();
        var path = ContractFiles.PathOf(FILE);
        if (Environment.GetEnvironmentVariable("UPDATE_LAYOUTS_DOC") == "1")
        {
            File.WriteAllText(path, expected);
        }

        var actual = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;

        actual.ShouldBe(expected, $"src/contracts/{FILE} is missing or out of date: regenerate it with `{REGENERATE}` and commit it");
    }

    private static string Render()
    {
        var layouts = new JsonArray();
        foreach (var layout in LayoutCatalogue.Layouts)
        {
            var slots = new JsonArray();
            foreach (var field in layout.Slots.Fields)
            {
                slots.Add(new JsonObject
                {
                    ["name"] = field.Name,
                    ["kind"] = field.Type.Kind,
                    ["required"] = field.Required,
                });
            }

            layouts.Add(new JsonObject
            {
                ["name"] = layout.Name,
                ["surface"] = layout.Surface.ToString().ToLowerInvariant(),
                ["tracker"] = layout.Tracker,
                ["slots"] = slots,
            });
        }

        var root = new JsonObject
        {
            ["generated"] = $"From LayoutCatalogue; do not edit by hand. Regenerate: {REGENERATE}",
            ["layouts"] = layouts,
        };
        return root.ToJsonString(_indented).ReplaceLineEndings("\n") + "\n";
    }
}
