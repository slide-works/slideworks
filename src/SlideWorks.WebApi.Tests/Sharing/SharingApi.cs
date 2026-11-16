using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SlideWorks.WebApi.Tests.Sharing;

// The calls the sharing tests make through the public API: make a deck as the owner, and read or change its sharing.
public static class SharingApi
{
    public static async Task<string> CreateDeckAsync(HttpClient owner, string title = "Ship smaller")
    {
        var response = await owner.PostAsJsonAsync("/api/decks", new { title });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    public static Task<HttpResponseMessage> PutAsync(HttpClient client, string deckId, object body)
        => client.PutAsJsonAsync($"/api/decks/{deckId}/sharing", body);

    public static async Task<JsonElement> PutOkAsync(HttpClient owner, string deckId, string level, params string[] viewers)
    {
        var response = await PutAsync(owner, deckId, new { level, viewers });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
