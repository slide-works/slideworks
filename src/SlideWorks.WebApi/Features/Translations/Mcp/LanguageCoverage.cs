namespace SlideWorks.WebApi.Features.Translations.Mcp;

// How complete one language is: its code and name, the number of lines, and how many are current, stale or missing.
public sealed record LanguageCoverage(string Code, string Name, int Lines, int Current, int Stale, int Missing);
