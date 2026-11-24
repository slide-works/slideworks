namespace SlideWorks.WebApi.Features.Sharing;

// The change log's sharing entries read "changed sharing: Named people (+alex@team.example)". Only the deck's owner (and the
// owner's agent) sees who was added or removed (D35); everyone else, writers included, reads "changed sharing".
public static class SharingActivity
{
    public const string PREFIX = "changed sharing: ";

    public const string REDACTED = "changed sharing";

    public static string Visible(string summary, bool isOwner)
        => !isOwner && summary.StartsWith(PREFIX, StringComparison.Ordinal) ? REDACTED : summary;
}
