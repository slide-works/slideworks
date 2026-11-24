using System.Net.Http.Json;
using System.Text.Json;

namespace SlideWorks.WebApi.Tests.Sharing;

// What the share limit tests send: whole-list shares through the sharing PUT, with recipients that no other test shares.
public static class ShareLimitApi
{
    // Fresh addresses; each test's sharer is its own approved person, so counts never meet.
    public static IReadOnlyList<string> NewEmails(int count) => [.. Enumerable.Range(0, count).Select(_ => $"r-{Guid.NewGuid():N}@recipients.example")];

    public static Task<HttpResponseMessage> ShareAsync(HttpClient sharer, string deck, params IEnumerable<string> viewers)
        => SharingApi.PutAsync(sharer, deck, new { level = "named", viewers = viewers.ToArray() });

    public static Task<HttpResponseMessage> ShareWithAccessAsync(HttpClient sharer, string deck, string access, params IEnumerable<string> emails)
        => SharingApi.PutAsync(sharer, deck, new { level = "named", people = emails.Select(email => new { email, access }).ToArray() });

    public static async Task<string> ErrorAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;

    public static async Task<string[]> ViewersAsync(HttpClient sharer, string deck)
        => (await sharer.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("viewers")
            .EnumerateArray().Select(v => v.GetString()!).Order().ToArray();
}
