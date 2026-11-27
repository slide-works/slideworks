using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The word-counting rule of a word-cloud round, shared by the presenter's reader and the print view. A word is its text key
// (D2): its count is the guests holding it in rows not hidden, and a word whose every row is hidden is Hidden and counts
// those rows' guests. Words go by count descending, then by key (D11). Guests counts guests with a word not hidden.
public static class WordCloudCounts
{
    public static int GuestsOf(IReadOnlyList<OpenEntry> entries)
        => entries.Where(e => e.HiddenAt == null).Select(e => e.GuestId).Distinct(StringComparer.Ordinal).Count();

    public static IReadOnlyList<CloudWord> WordsOf(IReadOnlyList<OpenEntry> entries)
        => [.. entries
            .GroupBy(e => e.TextKey, StringComparer.Ordinal)
            .Select(WordOf)
            .OrderByDescending(w => w.Word.Count)
            .ThenBy(w => w.Key, StringComparer.Ordinal)
            .Select(w => w.Word)];

    // The casing shown is the earliest sent row's (by arrival, then position) for the key.
    private static (string Key, CloudWord Word) WordOf(IGrouping<string, OpenEntry> rows)
    {
        var shown = rows.Where(e => e.HiddenAt == null).ToList();
        var counted = shown.Count > 0 ? shown : rows.ToList();
        var text = rows.OrderBy(e => e.Arrival).ThenBy(e => e.Position).ThenBy(e => e.Text, StringComparer.Ordinal).First().Text;
        return (rows.Key, new CloudWord(text, counted.Select(e => e.GuestId).Distinct(StringComparer.Ordinal).Count(), shown.Count == 0));
    }
}
