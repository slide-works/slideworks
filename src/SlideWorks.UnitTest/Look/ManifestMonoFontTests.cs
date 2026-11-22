using System.Text.Json.Nodes;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// A manifest's mono font is a role of a fonts[] entry (T3), not a key.
public class ManifestMonoFontTests
{
    [Test]
    public void Manifest_WhenMonoIsSet_ShouldRoundTrip()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.sample.json")))!.AsObject();
        var mono = manifest["fonts"]![0]!.DeepClone().AsObject();
        mono["role"] = "mono";
        mono["family"] = "Northwind Mono";
        manifest["fonts"]!.AsArray().Add(mono);

        var parsed = ManifestJson.Parse(manifest.ToJsonString());

        parsed.Errors.ShouldBeEmpty();
        JsonNode.DeepEquals(JsonNode.Parse(ManifestJson.Write(parsed.Manifest!)), manifest).ShouldBeTrue();
    }
}
