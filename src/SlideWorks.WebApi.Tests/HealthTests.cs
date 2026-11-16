using System.Net;
using System.Net.Http.Json;
using SlideWorks.WebApi.Tests.Dtos;

namespace SlideWorks.WebApi.Tests;

[TestFixture]
public class HealthTests : ApiTestFixture
{
    [Test]
    public async Task Health_WhenCalledAnonymously_Returns200WithVersion()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        body!.Status.ShouldBe("ok");
        body.Version.ShouldNotBeEmpty();
    }

    [Test]
    public async Task UnknownApiRoute_WhenRequested_Returns404RatherThanTheSpaShell()
    {
        var response = await Client.GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
