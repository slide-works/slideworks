namespace SlideWorks.WebApi.Features.Notes;

// The sample feature: entity, service, controller and view model in one folder,
// grouped by feature rather than by class type. Replace it with the real thing.
public class Note
{
    public int Id { get; set; }

    public required string Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
