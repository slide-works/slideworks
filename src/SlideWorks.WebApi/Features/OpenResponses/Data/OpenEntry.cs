namespace SlideWorks.WebApi.Features.OpenResponses.Data;

// One word or one answer a guest sent in a round. Position is the word's place in the guest's submit (0 for an answer).
// Text is as stored, collapsed, and TextKey is OpenTextRules.KeyOf(Text): a word's count and its hide both go by the key
// (D2, D11). A hidden entry has HiddenAt set and stays on the control view only.
public class OpenEntry
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public required string GuestId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public required string TextKey { get; set; }
    public DateTimeOffset? HiddenAt { get; set; }
}
