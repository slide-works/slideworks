using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests;

// Shared plumbing for black-box API fixtures: each fixture gets its own app (and temp SQLite file) and a client signed in
// as SignedInAs. Prerequisite data is created through the public API only, never by reaching into the database.
public abstract class ApiTestFixture
{
    private string _dbPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;

    // Signed in as SignedInAs.
    protected HttpClient Client { get; private set; } = null!;
    protected WebApplicationFactory<ApiMarker> Factory => _factory;

    // Who Client is signed in as. Anonymous means Client sends no sign-in at all.
    protected virtual TestUser SignedInAs => TestUser.Sam;

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
        _factory = TestAppFactory.Create(_dbPath, Clock, ExtraConfig, NamedHandlers, ConfigureTestServices);
        Client = ClientFor(SignedInAs);
    }

    [OneTimeTearDown]
    public void DisposeAppAndDeleteDatabase()
    {
        Client.Dispose();
        _factory.Dispose();
        TestAppFactory.DeleteDatabase(_dbPath);
    }

    // A client for another person (or Anonymous), as a browser on SlideWorks' own pages would be: every request carries
    // the public Origin, so writes pass the cross-site write guard. A test of the guard sets its own Origin per request.
    // The caller disposes it.
    protected HttpClient ClientFor(TestUser user)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Origin", TestAppFactory.PUBLIC_URL);
        if (!user.IsAnonymous)
        {
            client.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, user.Email);
        }
        return client;
    }
}
