namespace SlideWorks.Core.Rendering;

// What a render token lets a headless browser do (L9): Preview opens one deck's slide render pages for the
// agent's own screenshots; Card lets the server's card engine render that deck's share card; Print lets the PDF engine
// read one deck's print view.
public enum RenderPurpose
{
    Preview,
    Card,
    Print,
}
