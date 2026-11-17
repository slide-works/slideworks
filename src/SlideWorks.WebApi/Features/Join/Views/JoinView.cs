namespace SlideWorks.WebApi.Features.Join.Views;

// The join object (J1) carried by the play view, the deck page and the render page: how the room reaches the deck. Code is
// the deck's standing six-digit code, CodeLabel the same in two triples, JoinUrl the link the QR code holds, HostLabel what
// a slide prints and Live whether a session is open. It is sent to every viewer who can see the deck.
public sealed record JoinView(string Code, string CodeLabel, string JoinUrl, string HostLabel, bool Live);
