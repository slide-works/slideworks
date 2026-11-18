using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace SlideWorks.LoadTest;

// SW-9's run (Q10): each guest reads the questions, upvotes three of the open ones (one PUT each, carrying its revision) and
// taps ten reactions inside the window. It measures the write of every PUT and tap and, for each upvote, the time from its
// 200 to the first questionTally on the guest's own connection whose votes include it. The app needs a live deck with
// questions on; the harness asks --questions questions of its own first, each from a guest that is not one of the run's.
public sealed class QaScenario(LoadOptions options, HttpClient http) : ILoadScenario
{
    private readonly RunState _state = new(0);
    private readonly QaTotals _totals = new();
    private QaBaseline? _baseline;

    public string Action => "upvoting and tapping";

    public async Task PrepareAsync(CancellationToken ct)
    {
        _totals.QuestionsAsked = await AskSeedQuestionsAsync(ct);
        var view = await ReadFreshViewAsync(ct);
        if (view.Room?.Round is not { Length: > 0 } round)
        {
            throw new InvalidOperationException(
                $"There is no open Q&A round (questions are {view.Room?.Questions ?? "unknown"}). Go live with the deck's questions on, and run again.");
        }
        if (!view.Room.Reactions)
        {
            throw new InvalidOperationException("The deck's reactions setting is off, so the taps would only measure refusals. Turn reactions on, and run again.");
        }
        var open = (view.Questions ?? []).Where(q => q.State == "open").ToDictionary(q => q.Number, q => q.Votes);
        if (open.Count == 0)
        {
            throw new InvalidOperationException("The Q&A round has no open question to upvote. Pass --questions above 0, or ask some, and run again.");
        }
        _totals.OpenQuestions = open.Count;
        _baseline = new QaBaseline(round, open);
    }

    public ILoadGuest CreateGuest(Uri hubBase) => new QaGuest(options, hubBase, http, _state, _totals, _baseline ?? throw new InvalidOperationException("PrepareAsync has not run."));

    public IRunReport Report() => QaReport.From(options, _state, _totals);

    // One guest per question: the app lets a guest ask three a minute, and a guest of its own keeps the asker's upvote from
    // landing on a guest of the run.
    private async Task<int> AskSeedQuestionsAsync(CancellationToken ct)
    {
        var asked = 0;
        for (var i = 1; i <= options.Questions; i++)
        {
            var cookie = NewCookie();
            await using var hub = await JoinAsync(cookie, ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/join/{options.Code}/questions")
            {
                Content = JsonContent.Create(new { text = $"Load question {i}: how small is small enough?", name = (string?)null }),
            };
            request.Headers.Add("Cookie", cookie);
            using var response = await http.SendAsync(request, ct);
            if ((int)response.StatusCode != 201)
            {
                throw new InvalidOperationException($"POST /api/join/{options.Code}/questions answered {(int)response.StatusCode} for question {i}. Is the code live, with questions open?");
            }
            asked++;
        }
        return asked;
    }

    private async Task<QaWire.View> ReadFreshViewAsync(CancellationToken ct)
    {
        var cookie = NewCookie();
        await using var hub = await JoinAsync(cookie, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/questions");
        request.Headers.Add("Cookie", cookie);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GET /api/join/{options.Code}/questions answered {(int)response.StatusCode}. Is the code live?");
        }
        return await response.Content.ReadFromJsonAsync<QaWire.View>(QaGuest.JSON, ct)
            ?? throw new InvalidOperationException("GET questions answered no body.");
    }

    // The app gives the questions only to a guest on the session's roster, so a guest joins the audience first.
    private async Task<HubConnection> JoinAsync(string cookie, CancellationToken ct)
    {
        var hub = Presenter.Hub(options.Url, cookie);
        try
        {
            await hub.StartAsync(ct);
            await hub.InvokeAsync<JsonElement>("JoinAudience", options.Code, ct);
            return hub;
        }
        catch
        {
            await hub.DisposeAsync();
            throw;
        }
    }

    private static string NewCookie() => $"sw_guest={Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
}
