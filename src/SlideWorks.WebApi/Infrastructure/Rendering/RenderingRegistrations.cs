using System.Security.Cryptography;
using SlideWorks.Core.Rendering;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// Registers Core's RenderTokens (L9), which takes plain options rather than IConfiguration, so it can't be wired by an
// Agoda.IoC attribute. Its signing key is RenderTokens:Key (the environment variable RenderTokens__Key), at least 32
// characters. The key is required outside Development, and Program.cs resolves IRenderTokens at startup so a missing or
// short key fails there rather than on the first render. In Development with no key, each process makes a random one:
// tokens live at most 10 minutes and nothing outside the process checks them.
public static class RenderingRegistrations
{
    private const int DEVELOPMENT_KEY_BYTES = 32;

    public static IServiceCollection AddSlideWorksRendering(this IServiceCollection services) =>
        services.AddSingleton<IRenderTokens>(provider => new RenderTokens(
            OptionsFrom(provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<IHostEnvironment>()),
            provider.GetRequiredService<TimeProvider>()));

    private static RenderTokenOptions OptionsFrom(IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(RenderTokenOptions.SECTION).Get<RenderTokenOptions>() ?? new RenderTokenOptions();
        if (!string.IsNullOrEmpty(options.Key))
        {
            return options;
        }
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{RenderTokenOptions.SECTION}:Key is required outside Development. Set the environment variable {RenderTokenOptions.SECTION}__Key to a random secret of at least {RenderTokenOptions.MINIMUM_KEY_LENGTH} characters.");
        }
        options.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(DEVELOPMENT_KEY_BYTES));
        return options;
    }
}
