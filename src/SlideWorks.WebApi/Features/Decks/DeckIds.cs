using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SlideWorks.WebApi.Features.Decks;

// Deck ids and slugs (D13). The id is 12 random base62 characters and is the only id in every URL, API, tool and hub
// call. The slug is cosmetic: it comes from the title and is never looked up.
public static class DeckIds
{
    public const int LENGTH = 12;

    private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int SLUG_MAX_LENGTH = 60;
    private const string EMPTY_SLUG = "deck";

    public static string New() => RandomNumberGenerator.GetString(ALPHABET, LENGTH);

    // Lower-case ASCII letters and digits joined by single hyphens, with accents dropped ("Ship smaller, café!" becomes
    // "ship-smaller-cafe"), cut to 60 characters at a hyphen where possible. A title with nothing left gives "deck".
    public static string Slugify(string title)
    {
        var slug = new StringBuilder();
        var pendingHyphen = false;
        foreach (var c in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingHyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }
                slug.Append(char.ToLowerInvariant(c));
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }
        var text = slug.ToString();
        if (text.Length > SLUG_MAX_LENGTH)
        {
            var cut = text.LastIndexOf('-', SLUG_MAX_LENGTH);
            text = cut > 0 ? text[..cut] : text[..SLUG_MAX_LENGTH];
        }
        return text.Length == 0 ? EMPTY_SLUG : text;
    }
}
