using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// One checklist row, and what stops a publish. Blocks is true for a fail and for a todo that only a person can finish.
// Code, Path and Message describe the refusal a blocking row makes; they are null on a row that does not block.
public sealed record CheckOutcome(TemplateChecklistRow Row, bool Blocks, string? Code = null, string? Path = null, string? Message = null)
{
    public static CheckOutcome Pass(string key, string label, string detail, string tab)
        => new(new TemplateChecklistRow(key, label, TemplateCheckState.Pass, detail, tab), false);

    public static CheckOutcome Todo(string key, string label, string detail, string tab)
        => new(new TemplateChecklistRow(key, label, TemplateCheckState.Todo, detail, tab), false);

    public static CheckOutcome Fail(string key, string label, string tab, string code, string? path, string message)
        => new(new TemplateChecklistRow(key, label, TemplateCheckState.Fail, message, tab), true, code, path, message);
}
