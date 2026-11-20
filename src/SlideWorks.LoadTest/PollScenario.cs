using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace SlideWorks.LoadTest;

// Today's run, as it was: one answer per voter on an open poll round, and the time to the first tally that includes it.
public sealed class PollScenario(LoadOptions options, HttpClient http) : ILoadScenario
{
    private RunState? _state;

    public string Action => "answering";

    // The run's samples; set by PrepareAsync. The presenter's close line reads the acknowledged count from it.
    public RunState State => _state ?? throw new InvalidOperationException("PrepareAsync has not run.");

    public async Task PrepareAsync(CancellationToken ct) => _state = new RunState(await BaselineAsync(ct));

    public ILoadGuest CreateGuest(Uri hubBase) => new Voter(options, hubBase, http, State);

    public IRunReport Report() => LoadReport.From(options, State);

    // A fresh guest reads the poll first: a run against a poll that is not open would only measure refusals, so it stops, and
    // the tally it finds is what the voters' answers are counted from.
    private async Task<int> BaselineAsync(CancellationToken ct)
    {
        var cookie = $"sw_guest={Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))}";
        // The app gives the poll view only to a guest on the session's roster, so this guest joins the audience first.
        await using var hub = Presenter.Hub(options.Url, cookie);
        await hub.StartAsync(ct);
        await hub.InvokeAsync<System.Text.Json.JsonElement>("JoinAudience", options.Code, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/poll");
        request.Headers.Add("Cookie", cookie);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GET /api/join/{options.Code}/poll answered {(int)response.StatusCode}. Is the code live?");
        }
        var view = await response.Content.ReadFromJsonAsync<PollWire.View>(PollWire.JSON, ct);
        if (view?.Activity is not { Phase: "open", Round: not null })
        {
            throw new InvalidOperationException($"The poll is not open (phase {view?.Activity?.Phase ?? "none"}). Open a poll round on the deck and run again.");
        }
        // The tally a guest gets follows the poll's results setting. Under any other the thresholds would start from 0 and the
        // results would read low, so the run is refused unless tallies are not expected at all.
        if (!options.NoResults && !string.Equals(view.Activity.Results, "live", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The poll's results setting is '{view.Activity.Results ?? "unknown"}', not 'live', so the results timing would read low. Set the poll to live results, or pass {LoadOptions.NO_RESULTS}.");
        }
        return view.Tally?.Answered ?? 0;
    }
}
