namespace SlideWorks.Core.Look.CustomLayouts;

// One entry of a compiled variant's map: a value of the enum and the finished class it adds
// (cl-<scope>-<layout name>__is-<value>).
public sealed record VariantClass(string Value, string Class);
