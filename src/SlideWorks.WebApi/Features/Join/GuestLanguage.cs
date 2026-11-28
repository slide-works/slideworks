namespace SlideWorks.WebApi.Features.Join;

// One language in a guest's picker (SW-883 K5): its code, its own name, its English name, the chip's short label and how
// complete the deck's translation into it is, 0 to 100 (100 for the primary).
public sealed record GuestLanguage(string Code, string Name, string EnglishName, string Short, int Complete);
