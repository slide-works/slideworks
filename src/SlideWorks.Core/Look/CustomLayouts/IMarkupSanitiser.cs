namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 1: turns an uploaded layout.html into the checked tree, or says what is wrong with it. The
// tree has no scope yet and keeps the author's bare class names: the pipeline writes the prefix. Tree is null exactly when there are errors.
public interface IMarkupSanitiser
{
    (LayoutTree? Tree, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string markup);
}
