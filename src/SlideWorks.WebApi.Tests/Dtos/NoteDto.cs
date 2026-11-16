namespace SlideWorks.WebApi.Tests.Dtos;

// The tests' own copy of the API contract, so a change to the view model that
// breaks the wire shape fails here rather than passing by sharing the type.
public record NoteDto(int Id, string Text, string Created);
