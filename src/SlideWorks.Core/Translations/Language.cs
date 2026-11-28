namespace SlideWorks.Core.Translations;

// One language a deck can be translated into (K1): its code (case-sensitive), its name in itself, its
// name in English, the short label of the phone's chip, and the script whose font and fit it uses.
public sealed record Language(string Code, string Name, string EnglishName, string Short, string Script);
