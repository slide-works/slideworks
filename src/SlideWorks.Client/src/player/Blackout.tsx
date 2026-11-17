// Covers the stage in black while the presentation is blacked out (P8). It takes no clicks of its own, so the
// stage under it behaves as it did.
export function Blackout() {
  return (
    <div
      data-testid="blackout"
      aria-hidden="true"
      className="pointer-events-none absolute inset-0 bg-black"
    />
  );
}
