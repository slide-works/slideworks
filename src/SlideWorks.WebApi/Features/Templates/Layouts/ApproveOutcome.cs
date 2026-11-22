namespace SlideWorks.WebApi.Features.Templates.Layouts;

// What approving a custom layout came to. Only Approved writes anything.
public enum ApproveOutcome
{
    Approved,

    // The draft has no layout of that name.
    NotFound,

    // The report's asset is not the draft layout's current asset: the owner looked at an older upload.
    Changed,

    Overflows,

    LowContrast,

    // The template is archived: nothing about it changes any more.
    Archived,
}
