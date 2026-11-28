namespace SlideWorks.WebApi.Features.Join;

// The languages a guest can read the slides in (SW-883 K5): guest.json's "translations", null while translations are off.
// Languages lists the primary first, then the deck's list in order.
public sealed record GuestTranslations(string Primary, IReadOnlyList<GuestLanguage> Languages);
