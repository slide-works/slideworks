namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// The body of PUT /api/join/{code}/slides/{slide}/answer (open-text.json "request"): the round and revision the phone saw,
// and the answer as typed.
public record TextRequest(string Round, int Revision, string Text);
