using System.Globalization;
using System.Text.Encodings.Web;
using SlideWorks.WebApi.Infrastructure.Mail;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// The one share email (C8, D30). It has no free text and does not say whether the person can view or edit. The sharer's
// display name and the deck title are text other people chose, so each is cleaned before use: control characters, line
// breaks, and every Unicode format character (bidi overrides and isolates among them) are removed, then it is cut to
// NAME_MAX or TITLE_MAX characters. The HTML part encodes every value with HtmlEncoder.Default; this is the only place
// email HTML is built. The sharer's email address sits beside the name so a name can't pose as someone else.
public static class ShareEmailTemplate
{
    public const int NAME_MAX = 60;

    public const int TITLE_MAX = 80;

    private const string FALLBACK_NAME = "Someone";

    private const string FALLBACK_TITLE = "Untitled deck";

    public static MailMessageModel Build(string to, string sharerName, string sharerEmail, string deckTitle, string deckUrl, string unsubscribeUrl)
    {
        var name = Clean(sharerName, NAME_MAX, FALLBACK_NAME);
        var email = Clean(sharerEmail, 254, "");
        var title = Clean(deckTitle, TITLE_MAX, FALLBACK_TITLE);
        var subject = $"{name} shared a deck with you";
        var who = email.Length == 0 ? name : $"{name} ({email})";
        var text = $"{who} shared a deck with you.\n\n{title}\n{deckUrl}\n\nDon't want these emails? Unsubscribe: {unsubscribeUrl}\n";
        var encoder = HtmlEncoder.Default;
        var html = $"<p>{encoder.Encode(who)} shared a deck with you.</p>"
            + $"<p><a href=\"{encoder.Encode(deckUrl)}\">{encoder.Encode(title)}</a></p>"
            + $"<p>Don't want these emails? <a href=\"{encoder.Encode(unsubscribeUrl)}\">Unsubscribe</a></p>";
        return new MailMessageModel(to, subject, text, html, unsubscribeUrl);
    }

    // Removes every control (Cc), format (Cf, which holds the bidi characters), line and paragraph separator character, trims
    // the result, and cuts it to max characters without splitting a surrogate pair. Empty after cleaning gives the fallback.
    public static string Clean(string value, int max, string fallback)
    {
        var kept = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                continue;
            }
            kept.Append(c);
        }
        var cleaned = kept.ToString().Trim();
        if (cleaned.Length > max)
        {
            cleaned = cleaned[..(char.IsHighSurrogate(cleaned[max - 1]) ? max - 1 : max)].TrimEnd();
        }
        return cleaned.Length == 0 ? fallback : cleaned;
    }
}
