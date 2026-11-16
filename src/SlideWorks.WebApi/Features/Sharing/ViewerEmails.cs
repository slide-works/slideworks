using System.Net.Mail;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing;

// The one rule for named viewers' emails: trimmed and lower-cased with ToLowerInvariant, both when a DeckViewer row is
// written and when DeckAccess compares a caller's email with the list. SQLite's lower() folds ASCII only, so the
// comparison never relies on it.
public static class ViewerEmails
{
    public const int MAX_VIEWERS = 50;

    private const int MAX_LENGTH = 254;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // The list as stored: each email normalized and checked, duplicates dropped, in the order given. A bad one is a 400
    // that names it as the caller wrote it.
    public static IReadOnlyList<string> NormalizeAll(IReadOnlyList<string?> emails)
    {
        var normalized = new List<string>();
        foreach (var email in emails)
        {
            var trimmed = email?.Trim() ?? "";
            if (!IsEmail(trimmed))
            {
                throw new BadRequestException($"viewers: \"{trimmed}\" is not an email address");
            }
            var stored = trimmed.ToLowerInvariant();
            if (!normalized.Contains(stored, StringComparer.Ordinal))
            {
                normalized.Add(stored);
            }
        }
        return normalized.Count <= MAX_VIEWERS ? normalized : throw new BadRequestException($"viewers: at most {MAX_VIEWERS} emails");
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
