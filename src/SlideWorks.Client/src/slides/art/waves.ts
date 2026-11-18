// The waves title art (mockups section 09): fifteen sine paths in a 1920×1080
// box, each with its own opacity. Whoever draws them brings the <svg> and the
// stroke.
export const WAVE_PATHS = Array.from({ length: 15 }, (_, i) => {
  const y0 = 600 + i * 30;
  const amp = 16 + i * 2.4;
  let d = `M0 ${y0}`;
  for (let x = 24; x <= 1920; x += 24) {
    d += ` L${x} ${(y0 + Math.sin((x / 960) * 2 * Math.PI + i * 0.38) * amp).toFixed(1)}`;
  }
  return { d, opacity: 0.06 + i * 0.022 };
});
