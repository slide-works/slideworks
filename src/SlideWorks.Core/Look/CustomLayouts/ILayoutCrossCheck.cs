namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 4, run only when the first three passed: checks the tree against the schema and says how the
// layout reveals. Steps is null exactly when there are errors.
//
// The steps rule. A layout reveals in one of three ways: none, list or fixed, never both of the last two.
// - list: slots.json has steps: "L", and exactly one element that is not inside any data-each, at
//   any depth, carries data-each L with step 0 (data-step="each"). CustomSteps is (list, L, 0).
// - fixed: slots.json has no steps key, and the elements outside any data-each carry steps 1 to N
//   with no gap. CustomSteps is (fixed, null, N). N is never over CustomLayoutLimits.MAX_STEPS.
// - none: no steps key and no element with a step. CustomSteps is (none, null, 0).
// Anything else is steps_mismatch, which is also the code of a numbered step over the maximum (the
// pipeline refuses that one itself).
//
// A data-variant must name an enum slot where its element stands (CompiledTree says where that is),
// and every value of that enum must be a class name (lower-case letters, digits, hyphens), because
// the pipeline writes the class is-<value> for each: refuse anything else here, or the pipeline throws.
public interface ILayoutCrossCheck
{
    (CustomSteps? Steps, IReadOnlyList<CustomLayoutError> Errors) Check(LayoutTree tree, CustomLayoutSchema schema);
}
