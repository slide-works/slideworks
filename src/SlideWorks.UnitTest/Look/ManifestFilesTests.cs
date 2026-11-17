using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// ManifestFiles (T3): the one walk over a manifest's file fields, which storage and bundles share.
public class ManifestFilesTests
{
    private static TemplateManifest Sample() =>
        ManifestJson.Parse(File.ReadAllText(ContractFiles.PathOf("template-manifest.sample.json"))).Manifest!;

    [Test]
    public void Refs_WhenTheSampleIsRead_ShouldListEveryFileField()
    {
        var refs = ManifestFiles.Refs(Sample());

        refs.ShouldBe(
        [
            new ManifestFileRef("fonts[0].file", "font", "fonts/northwind-sans-700.woff2"),
            new ManifestFileRef("fonts[1].file", "font", "fonts/northwind-sans-800.woff2"),
            new ManifestFileRef("fonts[2].file", "font", "fonts/northwind-text-400.woff2"),
            new ManifestFileRef("fonts[3].file", "font", "fonts/northwind-mono-400.woff2"),
            new ManifestFileRef("logos.light", "logo", "logo-light.svg"),
            new ManifestFileRef("logos.dark", "logo", "logo-dark.svg"),
            new ManifestFileRef("titleArt.file", "art", "art.svg"),
            new ManifestFileRef("customLayouts[0].file", "layout", "layouts/stage-ladder"),
        ]);
    }

    [Test]
    public void Refs_WhenOptionalFilesAreNotSet_ShouldLeaveThemOut()
    {
        var sample = Sample();
        var bare = sample with
        {
            Fonts = [],
            Logos = new ManifestLogos { ShowOnDark = false },
            TitleArt = new ManifestTitleArt { Kind = "none" },
            CustomLayouts = [],
        };

        ManifestFiles.Refs(bare).ShouldBeEmpty();
        ManifestFiles.With(bare, new Dictionary<string, string>()).Logos.ShouldBe(bare.Logos);
    }

    [Test]
    public void With_WhenMapped_ShouldReplaceEveryFileField()
    {
        var sample = Sample();
        var before = ManifestFiles.Refs(sample);
        var map = before.ToDictionary(file => file.Value, file => "ast_" + file.Path);

        var mapped = ManifestFiles.With(sample, map);

        var after = ManifestFiles.Refs(mapped);
        after.Select(file => file.Value).ShouldBe(before.Select(file => "ast_" + file.Path));
        after.Select(file => (file.Path, file.Kind)).ShouldBe(before.Select(file => (file.Path, file.Kind)));
        ManifestJson.Write(mapped).ShouldNotContain(".woff2");
        ManifestJson.Write(mapped).ShouldNotContain(".svg");
        ManifestJson.Write(mapped).ShouldNotContain("layouts/stage-ladder");
        ManifestFiles.Refs(sample).ShouldBe(before);
        ManifestJson.Write(ManifestFiles.With(mapped, map.ToDictionary(pair => pair.Value, pair => pair.Key))).ShouldBe(ManifestJson.Write(sample));
    }

    [Test]
    public void With_WhenTheMapLacksAFile_ShouldThrow()
    {
        var sample = Sample();
        var map = ManifestFiles.Refs(sample).Skip(1).ToDictionary(file => file.Value, file => "ast_1");

        Should.Throw<KeyNotFoundException>(() => ManifestFiles.With(sample, map));
    }
}
