using Microsoft.AspNetCore.Mvc;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Tests.Templates;

// TemplateRefusal.Of, the one way a template service refuses, and the size limit on the bundle import action.
[TestFixture]
public class TemplateRefusalOfTests
{
    [Test]
    public void Of_WhenGivenACodeAndValues_ShouldCarryTheFrozenText()
    {
        var refusal = TemplateRefusal.Of(TemplateErrorCodes.FILE_TOO_LARGE, "fonts/inter.woff2", ("limit", "2 MB"));

        refusal.Code.ShouldBe("file_too_large");
        refusal.Message.ShouldBe("This file is too large. The limit is 2 MB.");
        refusal.Message.ShouldBe(TemplateErrorTexts.Format(TemplateErrorCodes.FILE_TOO_LARGE, ("limit", "2 MB")));
        refusal.Path.ShouldBe("fonts/inter.woff2");
        refusal.Errors.ShouldBeEmpty();
    }

    [Test]
    public void Of_WhenGivenOnlyACode_ShouldHaveNoPath()
    {
        var refusal = TemplateRefusal.Of(TemplateErrorCodes.UPLOAD_USED);

        refusal.Message.ShouldBe("This upload has already been used. Upload the file again.");
        refusal.Path.ShouldBeNull();
        refusal.Errors.ShouldBeEmpty();
    }

    [Test]
    public void Of_WhenGivenErrors_ShouldCarryThem()
    {
        TemplateRefusalDetail[] errors = [new("markup", "unknown_element", "The element is not allowed.")];

        var refusal = TemplateRefusal.Of(TemplateErrorCodes.MANIFEST_INVALID, errors, "layouts/ladder.html", ("reason", "see errors"));

        refusal.Message.ShouldBe("The template manifest is not valid: see errors");
        refusal.Path.ShouldBe("layouts/ladder.html");
        refusal.Errors.ShouldBe(errors);
    }

    [Test]
    public void Of_WhenAPlaceholderHasNoValue_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => TemplateRefusal.Of(TemplateErrorCodes.FILE_TOO_LARGE));
    }

    [Test]
    public void Of_WhenTheCodeIsUnknown_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => TemplateRefusal.Of("no_such_code"));
    }

    // Kestrel's default body limit (about 28.6 MB) is below a full bundle, so the import action has to carry its own.
    [Test]
    public void Import_WhenTheActionIsRead_ShouldCarryTheBundleSizeLimit()
    {
        var action = typeof(TemplateBundleController).GetMethod(nameof(TemplateBundleController.Import)).ShouldNotBeNull();

        var limit = action.GetCustomAttributesData().Single(attribute => attribute.AttributeType == typeof(RequestSizeLimitAttribute));

        limit.ConstructorArguments.ShouldHaveSingleItem().Value.ShouldBe((long)TemplateLimits.MAX_BUNDLE_BYTES);
        TemplateLimits.MAX_BUNDLE_BYTES.ShouldBe(60 * 1024 * 1024);
        BundleBodyAttribute.LIMIT.ShouldBe("60 MB");
    }
}
