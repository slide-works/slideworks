using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Live;

// J1: SLIDEWORKS_JOIN_URL is optional, an origin only, and checked at startup.
[TestFixture]
public class JoinUrlsTests
{
    [Test]
    public void JoinUrl_WhenNoJoinUrl_ShouldUseThePublicUrlWithAJPath()
    {
        var urls = Resolve(null);

        urls.ShouldBe(("http://localhost/j/482913", "http://localhost/join", "localhost/j"));
    }

    [Test]
    public void JoinUrl_WhenJoinUrlSet_ShouldUseItsOrigin()
    {
        var urls = Resolve("https://slideworks.live/");

        urls.ShouldBe(("https://slideworks.live/482913", "https://slideworks.live/join", "slideworks.live"));
    }

    [Test]
    public void HostLabel_WhenJoinUrlHasANonDefaultPort_ShouldKeepThePort()
    {
        var urls = Resolve("http://localhost:5511");

        urls.ShouldBe(("http://localhost:5511/482913", "http://localhost:5511/join", "localhost:5511"));
    }

    [TestCase("https://slideworks.live/talks")]
    [TestCase("https://slideworks.live/?x=1")]
    [TestCase("ftp://slideworks.live")]
    [TestCase("slideworks.live")]
    [TestCase("https://u:p@slideworks.live")]
    [TestCase("https://user@slideworks.live")]
    public void Startup_WhenJoinUrlHasPath_ShouldRefuse(string value)
    {
        var dbPath = TestAppFactory.NewDatabasePath();
        try
        {
            using var factory = TestAppFactory.Create(dbPath, new(), new() { [JoinUrls.CONFIG_KEY] = value });

            var error = Should.Throw<Exception>(() => factory.Services);

            Messages(error).ShouldContain(JoinUrls.CONFIG_KEY);
            Messages(error).ShouldNotContain("u:p");
        }
        finally
        {
            TestAppFactory.DeleteDatabase(dbPath);
        }
    }

    // Reads the three links inside the host's lifetime.
    private static (string JoinUrl, string Keypad, string HostLabel) Resolve(string? joinUrl)
    {
        var dbPath = TestAppFactory.NewDatabasePath();
        try
        {
            using var factory = TestAppFactory.Create(dbPath, new(), joinUrl == null ? null : new() { [JoinUrls.CONFIG_KEY] = joinUrl });
            var urls = factory.Services.GetRequiredService<IJoinUrls>();
            return (urls.JoinUrl("482913"), urls.KeypadUrl(), urls.HostLabel());
        }
        finally
        {
            TestAppFactory.DeleteDatabase(dbPath);
        }
    }

    private static string Messages(Exception error)
    {
        var messages = new List<string>();
        for (Exception? current = error; current != null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join(" | ", messages);
    }
}
