using System.ComponentModel.DataAnnotations;

namespace SlideWorks.WebApi.Features.Notes;

// [ApiController] answers 400 on its own when this fails validation, so the
// service never sees a blank note.
public record CreateNoteRequest([Required, MaxLength(NoteService.MAX_TEXT_LENGTH)] string Text);
