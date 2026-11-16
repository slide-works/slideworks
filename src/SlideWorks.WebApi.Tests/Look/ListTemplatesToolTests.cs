using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L7 list_templates: every template in the catalogue, with what an agent writes a deck to, read through ITemplateCatalogue.
[TestFixture]
public class ListTemplatesToolTests : ApiTestFixture
{
    private readonly LockingTemplateCatalogue _templates = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<ITemplateCatalogue>(_templates);

    [Test]
    public async Task ListTemplates_ShouldReturnEveryTemplateInTheCatalogueWithItsStructure()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var templates = await ListAsync(agent);

        templates.Select(t => t.GetProperty("name").GetString()).ShouldBe(_templates.All.Select(t => t.Name));
        foreach (var (entry, template) in templates.Zip(_templates.All))
        {
            entry.GetProperty("label").GetString().ShouldBe(template.Label);
            entry.GetProperty("purpose").GetString().ShouldBe(template.Purpose);
            entry.GetProperty("is_default").GetBoolean().ShouldBe(template.IsDefault);
            Strings(entry.GetProperty("layouts")).ShouldBe(template.Layouts);
            Strings(entry.GetProperty("outline")).ShouldBe(template.Outline);
            Strings(entry.GetProperty("house_style")).ShouldBe(template.HouseStyle);
            entry.GetProperty("chrome").ValueKind.ShouldBe(JsonValueKind.Object);
            entry.GetProperty("type").ValueKind.ShouldBe(JsonValueKind.Object);
        }
    }

    [Test]
    public async Task ListTemplates_ShouldNameTheLockedPaletteOnlyForALockingTemplate()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var templates = await ListAsync(agent);

        foreach (var entry in templates)
        {
            var locked = entry.GetProperty("locked_palette");
            if (entry.GetProperty("name").GetString() == LockingTemplateCatalogue.LOCKING)
            {
                locked.GetString().ShouldBe(_templates.Palette);
            }
            else
            {
                locked.ValueKind.ShouldBe(JsonValueKind.Null);
            }
        }
    }

    private static async Task<IReadOnlyList<JsonElement>> ListAsync(McpTestClient agent)
        => (await SlideCalls.OkAsync(agent, "list_templates", new { })).GetProperty("templates").EnumerateArray().ToList();

    private static IEnumerable<string?> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString());
}
