// The "Deep dive" pill on a slide flagged deep_dive, above the content box
// and under the part tracker. The frame draws the deep-dive top bar itself.
export function DeepDiveMark() {
  return (
    <span className="sw-dd-pill" data-testid="deep-dive-pill">
      Deep dive
    </span>
  );
}
