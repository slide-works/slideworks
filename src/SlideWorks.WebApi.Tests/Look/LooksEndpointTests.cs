using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L6, D20: GET /api/looks is the static catalogue, for signed-in people only, in the looks.json shape.
[TestFixture]
public class LooksEndpointTests : ApiTestFixture
{
    [Test]
    public async Task Get_WhenSignedOut_ShouldReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/looks");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Get_WhenSignedIn_ShouldListEveryBuiltInMatchingShape()
    {
        var response = await Client.GetAsync("/api/looks");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var looks = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(looks, "looks.json", ScreenShapes.Looks);
        looks.GetProperty("templates").EnumerateArray().Select(t => t.GetProperty("name").GetString())
            .ShouldBe(new TemplateCatalogue().All.Select(t => t.Name));
        looks.GetProperty("palettes").EnumerateArray().Select(p => p.GetProperty("name").GetString())
            .ShouldBe(new PaletteCatalogue().All.Select(p => p.Name));
        looks.GetProperty("templates").EnumerateArray().ShouldAllBe(t => t.GetProperty("lockedPalette").ValueKind == JsonValueKind.Null);
        looks.GetProperty("palettes").EnumerateArray().ShouldAllBe(p => p.GetProperty("contrast").GetProperty("pass").GetBoolean());
    }

    [Test]
    public async Task Get_ShouldSummariseEachPalettesContrastByItsClosestPair()
    {
        var looks = await Client.GetFromJsonAsync<JsonElement>("/api/looks");

        var lowest = looks.GetProperty("palettes").EnumerateArray()
            .ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("contrast").GetProperty("lowest"));
        lowest["cobalt"].GetProperty("pair").GetString().ShouldBe("panel-positive");
        lowest["cobalt"].GetProperty("ratio").GetDouble().ShouldBe(5.0);
        lowest["cobalt"].GetProperty("bar").GetDouble().ShouldBe(4.5);
        lowest["ember"].GetProperty("pair").GetString().ShouldBe("on-accent");
        lowest["ember"].GetProperty("ratio").GetDouble().ShouldBe(5.2);
    }
}
