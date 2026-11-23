using System.Net.Mail;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing;

// The one rule for named viewers' emails: trimmed and lower-cased with ToLowerInvariant, both when a DeckViewer row is
// written and when DeckAccess compares a caller's email with the list. SQLite's lower() folds ASCII only, so the
// comparison never relies on it.
public static class ViewerEmails
{
    public const int MAX_VIEWERS = 50;

    // The most entries a list may hold before any of them is looked at. Duplicates and the owner's own email don't count
    // towards MAX_VIEWERS, so a list may repeat itself a little, but never by enough to cost real work.
    public const int MAX_ENTRIES = MAX_VIEWERS * 4;

    public const string TOO_MANY = "viewers: at most 50 emails";

    public const string PEOPLE_TOO_MANY = "people: at most 50 emails";

    private const int MAX_LENGTH = 254;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // The list as stored: each email normalized and checked, duplicates and the owner's own email dropped, in the order
    // given. A bad one is a 400 that names it as the caller wrote it. The work is bounded: a list longer than MAX_ENTRIES
    // is refused before anything is normalized, and the walk stops as soon as it finds more than MAX_VIEWERS distinct emails.
    public static IReadOnlyList<string> NormalizeAll(IReadOnlyList<string?> emails, string? ownerEmail, string field = "viewers")
    {
        var tooMany = field == "viewers" ? TOO_MANY : PEOPLE_TOO_MANY;
        if (emails.Count > MAX_ENTRIES)
        {
            throw new BadRequestException(tooMany);
        }
        var owner = ownerEmail == null ? null : Normalize(ownerEmail);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>();
        foreach (var email in emails)
        {
            var trimmed = email?.Trim() ?? "";
            if (!IsEmail(trimmed))
            {
                throw new BadRequestException($"{field}: \"{trimmed}\" is not an email address");
            }
            var stored = trimmed.ToLowerInvariant();
            if (stored == owner || !seen.Add(stored))
            {
                continue;
            }
            normalized.Add(stored);
            if (normalized.Count > MAX_VIEWERS)
            {
                throw new BadRequestException(tooMany);
            }
        }
        return normalized;
    }

    // A bare address (no display name, no spaces) with a local part and a domain.
    private static bool IsEmail(string email)
        => email.Length is > 0 and <= MAX_LENGTH
            && !email.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(email, out var address)
            && address.Address == email
            && address.User.Length > 0
            && address.Host.Length > 0;
}
