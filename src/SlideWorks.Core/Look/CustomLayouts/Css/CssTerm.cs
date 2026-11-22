namespace SlideWorks.Core.Look.CustomLayouts.Css;

// One checked term of a value, already written in its output form. Name is a function's name or a
// keyword; for var(), IsToken and IsFont say which list the variable came from.
internal sealed record CssTerm(
    CssTermKind Kind,
    string Text,
    string Name = "",
    double Number = 0,
    string Unit = "",
    bool IsToken = false,
    bool IsFont = false);
