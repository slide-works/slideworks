using System.Collections.Frozen;

namespace SlideWorks.Core.OpenResponses;

// The built-in words that refuse a word-cloud or open-text submit (SW-8 D4). A word matches only as a whole word, after
// OpenTextRules has collapsed and case-folded it, so "Scunthorpe" or "class" never match.
public static class ProfanityList
{
    private static readonly FrozenSet<string> _words = new[]
    {
        "arse", "arsehole", "asshole", "bastard", "bitch", "bollocks", "bullshit", "cock", "cunt", "dickhead",
        "fuck", "fucked", "fucker", "fucking", "motherfucker", "piss", "prick", "shit", "shitty", "slut", "twat",
        "wanker", "whore",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Whether a collapsed, case-folded word is on the list.
    public static bool Contains(string word) => _words.Contains(word);
}
