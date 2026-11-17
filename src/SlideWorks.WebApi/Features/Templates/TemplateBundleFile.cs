namespace SlideWorks.WebApi.Features.Templates;

// An exported bundle: the zip's bytes, and the name to download it under, such as "northwind.zip".
public sealed record TemplateBundleFile(Stream Content, string FileName);
