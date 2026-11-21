using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// The fixture every poll test class that moves the presenter inherits: the app with SW-4's registry stood in by
// SettablePresentations. A class that swaps more services (a fake poll seam) adds them in ConfigurePollServices.
public abstract class PollTestFixture : ApiTestFixture
{
    protected sealed override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());
        ConfigurePollServices(services);
    };

    protected SettablePresentations Presentations => Factory.Services.GetRequiredService<SettablePresentations>();

    // Poll rows written straight to the database, for tests only (see PollRowSeed).
    protected PollRowSeed Rows => new(Factory.Services);

    // Further services to swap after the registry, such as a settable fake for another task's seam.
    protected virtual void ConfigurePollServices(IServiceCollection services)
    {
        // Nothing more by default.
    }

    // Puts the presenter on a slide as SW-4's registry does on an accepted change: records the state, then tells every
    // observer (the audience relay and, once it is real, the poll arrival rule).
    protected Task MoveAsync(string deckId, int slide, int step = 0) => PollSeed.MoveAsync(Factory.Services, deckId, slide, step);

    // What the poll, Q&A and slide-switch services do after they change something guests see (IAudienceRelay.RefreshAsync): the
    // relay answers a join from the last state it composed, so a test that writes rows or sets a double behind its back
    // calls this before the join reads, as the real change would have.
    protected Task RefreshAudienceAsync(string deckId) => Factory.Services.GetRequiredService<IAudienceRelay>().RefreshAsync(deckId);

    // A deck owned by SignedInAs with a poll slide, gone live, the presenter on the poll slide (see PollSeed).
    protected Task<SeededPoll> SeedPollAsync(string question = PollSeed.QUESTION, string[]? options = null, bool notify = false)
        => PollSeed.CreateAsync(Factory, Client, SignedInAs, question, options, notify);
}
