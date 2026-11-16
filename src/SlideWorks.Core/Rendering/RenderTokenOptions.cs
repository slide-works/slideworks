namespace SlideWorks.Core.Rendering;

// The secret that signs render tokens. RenderTokens refuses a key shorter than MINIMUM_KEY_LENGTH characters.
public sealed class RenderTokenOptions
{
    public const string SECTION = "RenderTokens";

    public const int MINIMUM_KEY_LENGTH = 32;

    public string Key { get; set; } = "";
}
