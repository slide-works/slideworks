using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Notes;

[Route("api/notes")]
public class NotesController(NoteService noteService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NoteViewModel>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<NoteViewModel>> List() => noteService.ListAsync();

    [HttpGet("{id:int}")]
    [ProducesResponseType<NoteViewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoteViewModel> Get(int id) => noteService.GetAsync(id);

    [HttpPost]
    [ProducesResponseType<NoteViewModel>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NoteViewModel>> Create(CreateNoteRequest request) =>
        CreatedAtGet(await noteService.CreateAsync(request));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Delete(int id) => NoContentAfter(noteService.DeleteAsync(id));
}
