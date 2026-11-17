using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Infrastructure.Setup;

namespace SlideWorks.WebApi.Tests.RateLimits;

// A SLIDEWORKS_TRUSTED_PROXIES entry that isn't a CIDR or a single address stops startup with a message naming it,
// rather than trusting nothing (or everything) without a word, and before the migrations touch the database.
[TestFixture]
public class TrustedProxiesStartupTests
{
    [TestCase("10.0.0.0/33")]
    [TestCase("proxy.internal")]
    [TestCase("10.0.0.0/8,,fd00::/8;")]
    [TestCase("10.0.0.0/8, fd00::/129")]
    public void Startup_WhenTrustedProxiesHasABadEntry_ShouldFail(string value)
    {
        var dbPath = TestAppFactory.NewDatabasePath();
        try
        {
            using var factory = TestAppFactory.Create(dbPath, new FakeTimeProvider(), new() { [TrustedProxiesSetup.CONFIG_KEY] = value });

            var error = Should.Throw<Exception>(() => factory.Services);

            AllMessages(error).ShouldContain($"{TrustedProxiesSetup.CONFIG_KEY} has '", Case.Sensitive);
            File.Exists(dbPath).ShouldBeFalse("a bad value must stop startup before the migrations create the database");
        }
        finally
        {
            TestAppFactory.DeleteDatabase(dbPath);
        }
    }

    private static string AllMessages(Exception error)
    {
        var messages = new List<string>();
        for (Exception? current = error; current != null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join(" | ", messages);
    }
}
