using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace SlideWorks.WebApi.Tests;

// With no SLIDEWORKS_API_TOKEN configured the app must fail closed: everything
// except /health is rejected, the SPA shell included.
[TestFixture]
public class FailClosedAuthTests
{
    private string _dbPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _dbPath = TestAppFactory.NewDatabasePath();
        _factory = TestAppFactory.Create(_dbPath, apiToken: "", new FakeTimeProvider());
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        TestAppFactory.DeleteDatabase(_dbPath);
    }

    [Test]
    public async Task Health_WhenNoTokenConfigured_StillReturns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("/")]
    [TestCase("/api/notes")]
    public async Task AnyOtherPath_WhenNoTokenConfigured_Returns401(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
