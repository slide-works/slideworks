namespace SlideWorks.WebApi.Features.Templates.Mcp;

// One row of a template's publish checklist as an agent reads it. Check is the row's key (palette, fonts, fontRights,
// logoArt, limits or layouts); Ok is true only when the row passes; Detail says what to fix, and is empty when there is
// nothing to say.
public sealed record TemplateCheck(string Check, bool Ok, string Detail);
