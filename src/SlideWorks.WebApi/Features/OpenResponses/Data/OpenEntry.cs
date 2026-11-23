namespace SlideWorks.WebApi.Features.OpenResponses.Data;

// One word or one answer a guest sent in a round. Position is the word's place in the guest's submit (0 for an answer).
// Text is as stored, collapsed, and TextKey is OpenTextRules.KeyOf(Text), or for a word one typo from another guest's word,
// Arrival is the entry's place in its round by when its guest first sent: one more than the round's highest when the guest's
// first entry is written, kept by every edit or replace, and shared by the rows of one guest's words. The wall, the control numbers and a word's
// first casing all order by it (a word row's ties go by Position). Rows from before it was added were numbered from their SQLite rowid.
// that word's key (SW-606): a word's count and its hide both go by the key (D2, D11). A hidden entry has HiddenAt set and stays on the control view only.
public class OpenEntry
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public required string GuestId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public required string TextKey { get; set; }
    public DateTimeOffset? HiddenAt { get; set; }
    public long Arrival { get; set; }
}
