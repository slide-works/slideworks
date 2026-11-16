type LaterButtonProps = { label: string; feature: string };

// A control a later Feature brings, shown disabled so the page's shape is
// already in place.
export function LaterButton({ label, feature }: LaterButtonProps) {
  return (
    <button
      type="button"
      disabled
      data-testid="deck-later"
      title={`Coming later (${feature})`}
      className="inline-flex cursor-not-allowed items-center rounded-panel border border-dashed border-edge-strong px-3 py-1.5 text-meta font-medium whitespace-nowrap text-ink-faint"
    >
      {label} · {feature}
    </button>
  );
}
