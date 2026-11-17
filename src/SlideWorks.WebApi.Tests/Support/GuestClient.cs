using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// An audience guest on the hub: anonymous, with a sw_guest cookie it mints itself (GuestCookie.NewId, the format the server
// issues and accepts), connected over the chosen transport. Pass noCookie for a guest the server has never
// seen. Tests of anything a guest does over the hub use this, never their own connection. Its HTTP requests carry the
// same cookie (or none) and the public Origin, as the guest's browser on the join page would.
public sealed class GuestClient : IAsyncDisposable
{
    private readonly HubReceiver _receiver;
    private readonly HttpClient _http;

    private GuestClient(HubReceiver receiver, HttpClient http, string guestId)
    {
        _receiver = receiver;
        _http = http;
        GuestId = guestId;
    }

    public string GuestId { get; }

    public HubConnection Connection => _receiver.Connection;

    public static async Task<GuestClient> ConnectAsync(
        WebApplicationFactory<ApiMarker> factory,
        HttpTransportType transport = HttpTransportType.WebSockets,
        string? guestId = null,
        bool noCookie = false)
    {
        var id = guestId ?? GuestCookie.NewId();
        var cookie = noCookie ? null : $"{GuestCookie.NAME}={id}";
        var receiver = await HubReceiver.StartAsync(factory, TestUser.Anonymous, transport, cookie);
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        http.DefaultRequestHeaders.Add("Origin", TestAppFactory.PUBLIC_URL);
        if (cookie != null)
        {
            http.DefaultRequestHeaders.Add("Cookie", cookie);
        }
        return new GuestClient(receiver, http, id);
    }

    public Task<JsonElement> NextAudienceAsync() => _receiver.NextAudienceAsync();

    // GET /api/join/{code}/poll: the poll on screen, this guest's answer and the tally it may see (V5).
    public Task<HttpResponseMessage> GetPollAsync(string code) => _http.GetAsync($"/api/join/{code}/poll");

    // PUT /api/join/{code}/poll/answer with this body, usually an AnswerRequest; any other object goes as it serialises,
    // for a test of a malformed body (V5).
    public Task<HttpResponseMessage> AnswerAsync(string code, object body) => _http.PutAsJsonAsync($"/api/join/{code}/poll/answer", body);

    // DeckHub.WatchPoll(code): joins poll-results:{roundId} when the server allows it, else throws HubException (V5).
    public Task WatchPollAsync(string code) => Connection.InvokeAsync("WatchPoll", code);

    // GET /api/join/{code}/questions: the room, the questions this guest may see and its own upvotes (Q4).
    public Task<HttpResponseMessage> GetQuestionsAsync(string code) => _http.GetAsync($"/api/join/{code}/questions");

    // POST /api/join/{code}/questions with this body, usually an AskRequest; any other object goes as it serialises, for a
    // test of a malformed body (Q4).
    public Task<HttpResponseMessage> AskAsync(string code, object body) => _http.PostAsJsonAsync($"/api/join/{code}/questions", body);

    // PUT /api/join/{code}/questions/votes with this body, usually a VoteRequest; any other object goes as it serialises (Q4).
    public Task<HttpResponseMessage> VoteAsync(string code, object body) => _http.PutAsJsonAsync($"/api/join/{code}/questions/votes", body);

    // POST /api/join/{code}/reactions with this body, usually a ReactionRequest; any other object goes as it serialises (Q7).
    public Task<HttpResponseMessage> ReactAsync(string code, object body) => _http.PostAsJsonAsync($"/api/join/{code}/reactions", body);

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _receiver.DisposeAsync();
    }
}
