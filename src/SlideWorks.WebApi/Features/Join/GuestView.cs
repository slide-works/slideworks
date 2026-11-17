using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Join;

// GET /api/join/{code} (src/contracts/screens/guest.json, J3): what a phone needs to follow the room. Never notes, viewers,
// the owner's email, the timer, a deep-dive mark or any slide but the one on screen (D15), which arrives inside State.
// DeckUrl is the player's root-relative link when the deck is shared by link, and null otherwise.
public sealed record GuestView(string DeckTitle, string PresenterName, LookView Look, string? DeckUrl, AudienceState State);
