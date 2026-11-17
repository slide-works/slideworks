namespace SlideWorks.WebApi.Features.Templates.Views;

// A published version of a template, with its publish day as a label.
public sealed record TemplateVersionView(int Version, string PublishedLabel);
