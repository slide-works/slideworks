using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlideWorks.WebApi.Features.Accounts.Admin;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Accounts.Admin;

// The startup warning when nobody can approve sign-ups. Serilog owns the logger factory, so the warning's logger is
// replaced for its own category rather than a provider being added.
[TestFixture]
public class NoAdminWarningTests
{
    private const string WARNING = "No admin exists and SLIDEWORKS_ADMIN_EMAIL is not set, so nobody can approve sign-ups. Set it and sign in with that Google account.";

    [Test]
    public async Task Startup_WhenNoAdminAndVariableUnset_ShouldLogWarning()
    {
        var logs = await StartAsync(adminEmail: "", signInAsAda: false);

        logs.Warnings.ShouldBe([WARNING]);
    }

    [Test]
    public async Task Startup_WhenVariableSet_ShouldNotWarn()
    {
        var logs = await StartAsync(adminEmail: TestUser.Ada.Email, signInAsAda: false);

        logs.Warnings.ShouldBeEmpty();
    }

    [Test]
    public async Task Startup_WhenAdminExists_ShouldNotWarn()
    {
        var logs = await StartAsync(adminEmail: TestUser.Ada.Email, signInAsAda: true, restartWith: "");

        logs.Warnings.ShouldBeEmpty();
    }

    // Starts a host (once, or twice for a restart on the same database) and returns what the second start logged.
    private static async Task<CapturedLogs> StartAsync(string adminEmail, bool signInAsAda, string? restartWith = null)
    {
        var dbPath = TestAppFactory.NewDatabasePath();
        try
        {
            if (signInAsAda)
            {
                using var first = TestAppFactory.Create(dbPath, new(), new() { [IUserDirectory.ADMIN_EMAIL_SETTING] = adminEmail });
                using var scope = first.Services.CreateScope();
                await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Ada);
            }
            var logs = new CapturedLogs();
            using var host = TestAppFactory.Create(
                dbPath,
                new(),
                new() { [IUserDirectory.ADMIN_EMAIL_SETTING] = restartWith ?? adminEmail },
                configureServices: services => services.AddSingleton<ILogger<NoAdminWarning>>(logs));
            _ = host.Services; // starts the host, and with it the hosted services
            return logs;
        }
        finally
        {
            TestAppFactory.DeleteDatabase(dbPath);
        }
    }

    private sealed class CapturedLogs : ILogger<NoAdminWarning>
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings => _warnings;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                _warnings.Add(formatter(state, exception));
            }
        }
    }
}
