namespace SlideWorks.Core.Slides;

// Collects what one validation pass finds: the failures, each naming its path, and the image ids
// that image slots point at.
public sealed class SlotValidation
{
    private readonly List<string> _errors = [];
    private readonly List<string> _imageIds = [];

    public IReadOnlyList<string> Errors => _errors;

    public IReadOnlyList<string> ImageIds => _imageIds;

    public void Fail(string path, string message) => _errors.Add($"{path}: {message}");

    public void AddImage(string imageId)
    {
        if (!_imageIds.Contains(imageId, StringComparer.Ordinal))
        {
            _imageIds.Add(imageId);
        }
    }
}
