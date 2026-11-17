namespace SlideWorks.WebApi.Features.Templates.Views;

// One row of the publish checklist. Key is palette, fonts, fontRights, logoArt, limits or layouts, in that order. Tab is
// the editor tab that fixes the row (palette, fonts, logo-art, basics, layouts). Detail is empty when there is nothing
// to say.
public sealed record TemplateChecklistRow(string Key, string Label, TemplateCheckState State, string Detail, string Tab);
