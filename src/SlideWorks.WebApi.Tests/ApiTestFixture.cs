using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace SlideWorks.WebApi.Tests;

// Shared plumbing for black-box API fixtures: each fixture gets its own app
// (and temp SQLite file) and an already-authenticated client. Prerequisite data
// is created through the public API only, never by reaching into the database.
public abstract class ApiTestFixture
{
    protected const string TEST_TOKEN = "integration-test-token";

    private string _dbPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;

    protected HttpClient Client { get; private set; } = null!;
    protected WebApplicationFactory<ApiMarker> Factory => _factory;

    // A fake clock, so anything the API stamps or formats is assertable. It starts at the real time (to the second), not a
    // fixed date: the API's stamps also reach code on the real clock (a cookie jar, a browser, the database), and a fixed
    // date plus a lifetime (a cookie, a token) breaks those tests once the calendar passes it. AG0051 reports fixed dates.
    protected FakeTimeProvider Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    protected virtual Dictionary<string, string?> ExtraConfig => [];

    // An outbound named client to fake at the HTTP boundary, by name.
    protected virtual Dictionary<string, HttpMessageHandler> NamedHandlers => [];

    // Any further service to swap on the test host after Program.cs has run.
    protected virtual Action<IServiceCollection>? ConfigureTestServices => null;

    [OneTimeSetUp]
    public void CreateAppAndClient()
    {
        _dbPath = TestAppFactory.NewDatabasePath();
        _factory = TestAppFactory.Create(
            _dbPath,
            TEST_TOKEN,
            Clock, ExtraConfig, NamedHandlers, ConfigureTestServices);
        Client = _factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TEST_TOKEN);
    }

    [OneTimeTearDown]
    public void DisposeAppAndDeleteDatabase()
    {
        Client.Dispose();
        _factory.Dispose();
        TestAppFactory.DeleteDatabase(_dbPath);
    }
}
