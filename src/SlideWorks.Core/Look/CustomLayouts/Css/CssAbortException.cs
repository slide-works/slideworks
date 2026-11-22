namespace SlideWorks.Core.Look.CustomLayouts.Css;

// Thrown inside the sanitiser to stop at once (parentheses too deep, too many rules). Always caught
// by CssSheetParser; it never leaves Core.
public sealed class CssAbortException : Exception
{
}
