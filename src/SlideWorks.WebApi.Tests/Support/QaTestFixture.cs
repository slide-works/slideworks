using Microsoft.Extensions.DependencyInjection;

namespace SlideWorks.WebApi.Tests.Support;

// The fixture every Q&A test class inherits: PollTestFixture's app (SettablePresentations as SW-4's registry), plus QaSeed
// and QaRowSeed. A class that swaps a Q&A seam for one of its doubles (FakeQaRounds.AddTo, FakeQaSettings.AddTo, ...) adds
// it in ConfigureQaServices; a poll double goes there too.
public abstract class QaTestFixture : PollTestFixture
{
    // SW-9's rows written straight to the database, for tests only (see QaRowSeed).
    protected QaRowSeed QaRowSeed => new(Factory.Services);

    protected sealed override void ConfigurePollServices(IServiceCollection services) => ConfigureQaServices(services);

    // The Q&A doubles (and any poll double) this class runs with.
    protected virtual void ConfigureQaServices(IServiceCollection services)
    {
        // Nothing more by default.
    }

    // A deck owned by SignedInAs with a quote and a qa slide, gone live, with an open Q&A round (see QaSeed).
    protected Task<SeededQa> SeedQaAsync() => QaSeed.CreateAsync(Factory, Client, SignedInAs);
}
