using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// L7 list_layouts {template?}: with a template, only the layouts it allows, in the catalogue's order; an unknown template
// is a tool error with the L3 text.
[TestFixture]
public class ListLayoutsByTemplateTests : ApiTestFixture
{
    [Test]
    public async Task ListLayouts_WhenTemplate_ShouldListOnlyItsLayouts()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        foreach (var template in new TemplateCatalogue().All)
        {
            var layouts = await NamesAsync(agent, new { template = template.Name });

            layouts.ShouldBe(LayoutCatalogue.Layouts.Select(l => l.Name).Where(template.Allows));
        }
    }

    [Test]
    public async Task ListLayouts_WhenNoTemplate_ShouldListEveryLayout()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        (await NamesAsync(agent, new { })).ShouldBe(LayoutCatalogue.Layouts.Select(l => l.Name));
    }

    [Test]
    public async Task ListLayouts_WhenUnknownTemplate_ShouldGiveTheL3ToolError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        (await SlideCalls.ErrorAsync(agent, "list_layouts", new { template = "nope" })).ShouldBe("Unknown template 'nope'");
    }

    private static async Task<IReadOnlyList<string?>> NamesAsync(McpTestClient agent, object args)
        => (await SlideCalls.OkAsync(agent, "list_layouts", args)).GetProperty("layouts").EnumerateArray().Select(l => l.GetProperty("name").GetString()).ToList();
}
