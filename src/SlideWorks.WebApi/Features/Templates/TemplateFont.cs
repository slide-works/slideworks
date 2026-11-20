namespace SlideWorks.WebApi.Features.Templates;

// One font file of a template (T4). Role is "head", "body" or "mono"; Asset is the font's asset id.
public sealed record TemplateFont(string Family, string Role, string Asset, int Weight, string Style);
