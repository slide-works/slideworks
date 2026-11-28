namespace SlideWorks.WebApi.Infrastructure;

// A refusal with a machine code and a sentence the server words for people (K4): { "error": "<code>", "message": "..." }.
// Clients show the message as given.
public record RefusalResponse(string Error, string Message);
