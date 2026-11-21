namespace SlideWorks.AcceptanceTests;

// One slide of a followed preview_deck result: its viewport PNG and the URL the page ended on.
public sealed record PreviewShot(byte[] Png, string Url);
