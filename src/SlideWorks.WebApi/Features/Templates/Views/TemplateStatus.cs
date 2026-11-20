namespace SlideWorks.WebApi.Features.Templates.Views;

// Where a template stands: only a draft so far, published at least once, or archived (hidden from the pickers).
public enum TemplateStatus
{
    Draft,
    Published,
    Archived,
}
