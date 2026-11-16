namespace SlideWorks.Core.Slides;

// Collects what one validation pass finds: the failures, each naming its path, and the image ids
// that image slots point at. It keeps at most MAX_ERRORS messages, so a hostile slide can't grow the
// error text (returned whole as the 400 body and the MCP tool error) with its own size.
public sealed class SlotValidation
{
    public const int MAX_ERRORS = 20;

    private readonly List<string> _errors = [];
    private readonly List<string> _imageIds = [];

    // Every failure counted, including those past MAX_ERRORS.
    public int ErrorCount { get; private set; }

    public IReadOnlyList<string> Errors => ErrorCount > _errors.Count
        ? [.. _errors, $"…and {ErrorCount - _errors.Count} more"]
        : _errors;

    public IReadOnlyList<string> ImageIds => _imageIds;

    public void Fail(string path, string message)
    {
        if (_errors.Count < MAX_ERRORS)
        {
            _errors.Add($"{path}: {message}");
        }

        ErrorCount++;
    }

    public void AddImage(string imageId)
    {
        if (!_imageIds.Contains(imageId, StringComparer.Ordinal))
        {
            _imageIds.Add(imageId);
        }
    }
}
