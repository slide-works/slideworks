using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using SlideWorks.WebApi.Tests.Dtos;

namespace SlideWorks.WebApi.Tests;

[TestFixture]
public class NotesApiTests : ApiTestFixture
{
    [Test]
    public async Task Create_WhenTextGiven_Returns201WithLocationAndServerFormattedDate()
    {
        var response = await Client.PostAsJsonAsync("/api/notes", new { text = "  First note  " });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        note!.Text.ShouldBe("First note");
        note.Created.ShouldBe(Clock.GetUtcNow().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/notes/{note.Id}");
    }

    [Test]
    public async Task Create_WhenTextBlank_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/notes", new { text = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task List_WhenSeveralNotesExist_ReturnsNewestFirst()
    {
        var older = await CreateNoteAsync("older");
        var newer = await CreateNoteAsync("newer");

        var notes = await Client.GetFromJsonAsync<List<NoteDto>>("/api/notes");

        notes!.Select(n => n.Id).Where(id => id == older.Id || id == newer.Id).ShouldBe([newer.Id, older.Id]);
    }

    [Test]
    public async Task Get_WhenNoteMissing_Returns404WithErrorBody()
    {
        var response = await Client.GetAsync("/api/notes/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Error.ShouldBe("Note 999999 not found.");
    }

    [Test]
    public async Task Delete_WhenNoteExists_Returns204AndTheNoteIsGone()
    {
        var note = await CreateNoteAsync("short-lived");

        var response = await Client.DeleteAsync($"/api/notes/{note.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/notes/{note.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<NoteDto> CreateNoteAsync(string text)
    {
        var response = await Client.PostAsJsonAsync("/api/notes", new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }
}
