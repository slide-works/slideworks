using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts;

// Compiles one uploaded custom layout (T10). The markup, schema and CSS stages all run, so the author
// sees every stage's errors at once; the cross-check runs only when all three passed. This is the only
// way an upload becomes a CompiledLayout.
public sealed class CustomLayoutPipeline(
    IMarkupSanitiser markup,
    ICssSanitiser css,
    ISlotSchemaCompiler schema,
    ILayoutCrossCheck crossCheck,
    ILayoutSamples samples)
{
    // scope is the first 8 characters of the upload's asset id: 8 lower-case hex characters.
    public CompileResult Compile(string markupText, string cssText, string slotsJson, string scope)
    {
        if (!LayoutScope.IsValid(scope))
        {
            throw new ArgumentException("A layout's scope is 8 lower-case hex characters.", nameof(scope));
        }

        var (tree, markupErrors) = markup.Sanitise(markupText);
        var (definition, compiledSchema, schemaErrors) = schema.Compile(slotsJson, tree is null ? 0 : TreeSteps.FixedCount(tree));
        var prefix = LayoutScope.ClassPrefix(scope, compiledSchema?.Name ?? LayoutScope.UNNAMED);
        var (scopedCss, cssErrors) = css.Sanitise(cssText, prefix, tree?.VarNames() ?? []);

        IReadOnlyList<CustomLayoutError> errors = [.. markupErrors, .. schemaErrors, .. cssErrors];
        if (errors.Count > 0)
        {
            return new CompileResult(null, errors);
        }

        if (tree is null || definition is null || compiledSchema is null || scopedCss is null)
        {
            throw new InvalidOperationException("A stage returned neither a result nor an error.");
        }

        var (steps, crossErrors) = crossCheck.Check(tree, compiledSchema);
        if (crossErrors.Count > 0)
        {
            return new CompileResult(null, crossErrors);
        }

        if (steps is null)
        {
            throw new InvalidOperationException("The cross-check returned neither steps nor an error.");
        }

        var sample = samples.Sample(compiledSchema);
        var maximum = samples.Maximum(compiledSchema);
        MustValidate(definition, sample, "sample");
        MustValidate(definition, maximum, "maximum");

        return new CompileResult(
            new CompiledLayout(
                CompiledLayout.COMPILER_VERSION,
                compiledSchema.Name,
                compiledSchema.Description,
                compiledSchema.Surface,
                steps,
                definition,
                tree with { Scope = scope },
                scopedCss,
                slotsJson,
                sample,
                maximum),
            []);
    }

    // Slots we wrote ourselves from a schema we accepted: a failure is our bug, not the author's.
    private static void MustValidate(LayoutDefinition definition, string slots, string which)
    {
        using var document = JsonDocument.Parse(slots);
        var validation = new SlotValidation();
        definition.Validate(document.RootElement, validation);
        if (validation.ErrorCount > 0)
        {
            throw new InvalidOperationException($"The {which} slots of layout '{definition.Name}' do not pass its own definition: {string.Join("; ", validation.Errors)}");
        }
    }
}
