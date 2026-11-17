using System.Text.Json;

namespace SlideWorks.WebApi.Realtime;

// The one slide on screen, as a guest gets it (J4, D15): the presenter's position projected onto that slide. Index is
// 1-based like the player's #slide.step and Step is 0-based. It never carries notes, a deep-dive mark or any other slide,
// so a private deck can't be read ahead. Slots are the slide's own slots, untouched.
public sealed record AudienceSlide(int Index, int Count, string Layout, JsonElement Slots, int Steps, int Step, bool Black);
