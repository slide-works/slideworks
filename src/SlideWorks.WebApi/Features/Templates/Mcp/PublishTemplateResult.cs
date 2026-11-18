namespace SlideWorks.WebApi.Features.Templates.Mcp;

// publish_template: Template is the tpl_ id, Version the number just published, Url the editor's address.
public sealed record PublishTemplateResult(string Template, int Version, string Url);
