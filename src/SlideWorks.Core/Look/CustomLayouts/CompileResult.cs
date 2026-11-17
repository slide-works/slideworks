namespace SlideWorks.Core.Look.CustomLayouts;

// What compiling an upload gives: the layout and no errors, or no layout and every error found.
public sealed record CompileResult(CompiledLayout? Layout, IReadOnlyList<CustomLayoutError> Errors);
