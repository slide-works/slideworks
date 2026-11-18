using System.Text.Json;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// The frozen error texts (T3): src/contracts/template-errors.json and TemplateErrorTexts.Format.
public class TemplateErrorTextsTests
{
    private static readonly string[] Codes =
    [
        "font_not_woff2", "font_width_out_of_range", "svg_script", "svg_external_reference", "svg_foreign_object",
        "svg_malformed", "not_an_image", "palette_contrast", "layout_not_approved", "layout_refused", "too_many_fonts",
        "too_many_layouts", "too_many_extras", "too_many_templates", "file_too_large", "rights_not_confirmed",
        "stale_revision", "manifest_invalid", "asset_not_found", "bundle_invalid", "bundle_too_large",
        "custom_layouts_unavailable", "renderer_unavailable", "upload_used", "not_published", "template_storage_full",
    ];

    [Test]
    public void Format_WhenEveryCodeIsAsked_ShouldLeaveNoPlaceholder()
    {
        TemplateErrorTexts.Codes.ShouldBe(Codes, ignoreOrder: true);

        foreach (var code in Codes)
        {
            var values = TemplateErrorTexts.Placeholders(code).Select(name => (name, "<" + name + ">")).ToArray();

            var text = TemplateErrorTexts.Format(code, values);

            text.ShouldNotBeNullOrWhiteSpace(code);
            text.ShouldNotContain("{", Case.Sensitive, code);
            text.ShouldNotContain("}", Case.Sensitive, code);
            foreach (var (_, value) in values)
            {
                text.ShouldContain(value, Case.Sensitive, code);
            }
        }
    }

    [Test]
    public void Format_WhenACodeHasPlaceholders_ShouldBeTheContractsNames()
    {
        TemplateErrorTexts.Placeholders("font_not_woff2").ShouldBe(["found"]);
        TemplateErrorTexts.Placeholders("font_width_out_of_range").ShouldBe(["problem", "width", "limit"]);
        TemplateErrorTexts.Placeholders("palette_contrast").ShouldBe(["pair", "ratio", "needed"], ignoreOrder: true);
        TemplateErrorTexts.Placeholders("file_too_large").ShouldBe(["limit"]);
        TemplateErrorTexts.Placeholders("bundle_invalid").ShouldBe(["reason"]);
        TemplateErrorTexts.Placeholders("manifest_invalid").ShouldBe(["reason"]);
        Codes.Count(code => TemplateErrorTexts.Placeholders(code).Count > 0).ShouldBe(6);
    }

    [Test]
    public void Format_WhenTheTextIsTheMockups_ShouldReadAsDrawn()
    {
        TemplateErrorTexts.Format("font_not_woff2", ("found", "TrueType")).ShouldBe("Fonts must be woff2 files. This one is TrueType.");
        TemplateErrorTexts.Format("font_width_out_of_range", ("problem", "too wide"), ("width", "0.74"), ("limit", "0.70"))
            .ShouldBe("This font is too wide for slide text (0.74 em; the limit is 0.70).");
        TemplateErrorTexts.Format("svg_script").ShouldBe("This SVG contains a script. Export it again without scripts, or upload a PNG.");
        TemplateErrorTexts.Format("layout_refused").ShouldBe("this layout was refused; see errors");
        TemplateErrorTexts.Format("custom_layouts_unavailable").ShouldBe("Custom layouts are not available yet.");
        TemplateErrorTexts.Format("rights_not_confirmed").ShouldStartWith("Confirm you can serve these fonts.");
    }

    [Test]
    public void Format_WhenTheCodeIsALimit_ShouldNameTheLimit()
    {
        const int MB = 1024 * 1024;

        TemplateErrorTexts.Format("too_many_fonts").ShouldContain($"{TemplateLimits.MAX_FONT_FILES} font files");
        TemplateErrorTexts.Format("too_many_layouts").ShouldContain($"{TemplateLimits.MAX_CUSTOM_LAYOUTS} custom layouts");
        TemplateErrorTexts.Format("too_many_extras").ShouldContain($"{TemplateLimits.MAX_EXTRAS} extra colours");
        TemplateErrorTexts.Format("too_many_templates").ShouldContain($"{TemplateLimits.MAX_TEMPLATES_PER_PERSON} templates");
        TemplateErrorTexts.Format("bundle_too_large").ShouldContain($"{TemplateLimits.MAX_BUNDLE_ENTRIES} files");
        TemplateErrorTexts.Format("bundle_too_large").ShouldContain($"{TemplateLimits.MAX_BUNDLE_BYTES / MB} MB");
        TemplateErrorTexts.Format("file_too_large", ("limit", "2 MB")).ShouldContain("The limit is 2 MB.");
    }

    [Test]
    public void Limits_WhenRead_ShouldBeTheContractsNumbers()
    {
        const int MB = 1024 * 1024;

        TemplateLimits.MAX_FONT_FILES.ShouldBe(8);
        TemplateLimits.MAX_FONT_BYTES.ShouldBe(2 * MB);
        TemplateLimits.MAX_IMAGE_BYTES.ShouldBe(10 * MB);
        TemplateLimits.MAX_CUSTOM_LAYOUTS.ShouldBe(12);
        TemplateLimits.MAX_LAYOUT_BYTES.ShouldBe(800_000);
        TemplateLimits.MAX_LAYOUT_PART_BYTES.ShouldBe(262_144);
        TemplateLimits.MAX_EXTRAS.ShouldBe(4);
        TemplateLimits.MAX_ORNAMENT_DOTS.ShouldBe(6);
        TemplateLimits.MAX_TEMPLATES_PER_PERSON.ShouldBe(50);
        TemplateLimits.MAX_BUNDLE_BYTES.ShouldBe(60 * MB);
        TemplateLimits.MAX_BUNDLE_ENTRIES.ShouldBe(64);
    }

    [Test]
    public void Format_WhenAValueIsMissing_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => TemplateErrorTexts.Format("font_width_out_of_range", ("problem", "too wide"), ("width", "0.74")))
            .Message.ShouldContain("{limit}");
        Should.Throw<ArgumentException>(() => TemplateErrorTexts.Format("file_too_large"));
    }

    [Test]
    public void Format_WhenTheCodeIsUnknown_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => TemplateErrorTexts.Format("no_such_code")).Message.ShouldContain("no_such_code");
    }

    [Test]
    public void File_WhenRead_ShouldHoldOnlyCodesAndTexts()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("template-errors.json")));

        file.RootElement.EnumerateObject().Select(entry => entry.Name).ShouldBe(Codes);
        file.RootElement.EnumerateObject().ShouldAllBe(entry => entry.Value.ValueKind == JsonValueKind.String);
    }
}
