using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Mcp;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// How the template routes and tools answer a refusal (T5), with the fakes told to refuse: 400 with the code, the
// message, the path when there is one and the list of findings; 409 for a stale revision; 404 as everywhere else. The
// refusals the real services make are tested where those services are built. A fake's refusal is for every call, so the
// tests of this fixture do not run in parallel and each one accepts calls again when it is done.
[TestFixture]
[NonParallelizable]
public class TemplateErrorsTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeTemplateDrafts.Register(services);
        FakeTemplatePublishing.Register(services);
        FakeTemplateSharing.Register(services);
    };

    private FakeTemplateDrafts Drafts => Factory.Services.GetRequiredService<FakeTemplateDrafts>();

    private FakeTemplatePublishing Publishing => Factory.Services.GetRequiredService<FakeTemplatePublishing>();

    [TearDown]
    public void AcceptCallsAgain()
    {
        Drafts.Refuse(null);
        Publishing.Refuse(null);
    }

    [Test]
    public async Task Update_WhenTheServiceRefusesWithAPath_ShouldAnswer400WithCodeMessageAndPath()
    {
        Drafts.Refuse(new TemplateRefusedException(TemplateErrorCodes.TOO_MANY_EXTRAS, "A palette can hold at most 4 extra colours.", "palette.extras"));

        var response = await Client.PutAsJsonAsync("/api/templates/tpl_refused00001", new { manifest = new { name = "refused" }, revision = 7 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe(
            """{"error":"too_many_extras","message":"A palette can hold at most 4 extra colours.","path":"palette.extras","errors":[]}""");
        Drafts.Calls().ShouldNotContain("Update tpl_refused00001 7");
    }

    // A refusal about the whole template has no path, and a file check's findings go in errors.
    [Test]
    public async Task Publish_WhenTheServiceRefusesWithFindingsAndNoPath_ShouldLeavePathOutAndListThem()
    {
        Publishing.Refuse(new TemplateRefusedException(TemplateErrorCodes.SVG_SCRIPT, "This SVG contains a script.")
        {
            Errors = [new TemplateRefusalDetail("logos.light", TemplateErrorCodes.SVG_SCRIPT, "line 4: <script>")],
        });

        var response = await Client.PostAsync("/api/templates/tpl_refused00002/publish", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe(
            """{"error":"svg_script","message":"This SVG contains a script.","errors":[{"path":"logos.light","code":"svg_script","message":"line 4: <script>"}]}""");
    }

    [Test]
    public async Task Update_WhenTheCodeIsStaleRevision_ShouldAnswer409()
    {
        Drafts.Refuse(new TemplateRefusedException(TemplateErrorCodes.STALE_REVISION, "This template has changed since you read it."));

        var response = await Client.PutAsJsonAsync("/api/templates/tpl_refused00003", new { manifest = new { name = "stale" }, revision = 3 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().ShouldBe("stale_revision");
        body.GetProperty("message").GetString().ShouldBe("This template has changed since you read it.");
        body.TryGetProperty("path", out _).ShouldBeFalse();
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Get_WhenTheServiceSaysNotFound_ShouldAnswer404()
    {
        Drafts.Refuse(new NotFoundException("Template not found"));

        var response = await Client.GetAsync("/api/templates/tpl_refused00004");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Template not found");
    }

    [Test]
    public async Task Run_WhenARefusalHasAPath_ShouldReportPathAndMessage()
    {
        var result = await McpToolErrors.Run<int>(
            () => throw new TemplateRefusedException(TemplateErrorCodes.FONT_NOT_WOFF2, "Fonts must be woff2 files. This one is ttf.", "fonts[1].file"));

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("fonts[1].file: Fonts must be woff2 files. This one is ttf.");
    }

    [Test]
    public async Task Run_WhenARefusalHasNoPath_ShouldReportTheMessage()
    {
        var result = await McpToolErrors.Run<int>(
            () => throw new TemplateRefusedException(TemplateErrorCodes.TOO_MANY_TEMPLATES, "You can have at most 50 templates. Archive one to make room."));

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("You can have at most 50 templates. Archive one to make room.");
    }
}
