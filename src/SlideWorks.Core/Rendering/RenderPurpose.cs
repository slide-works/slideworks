namespace SlideWorks.Core.Rendering;

// What a render token lets a headless browser do (L9): Preview opens one deck's slide render pages for the
// agent's own screenshots; Card lets the server's card engine render that deck's share card.
public enum RenderPurpose
{
    Preview,
    Card,
}
