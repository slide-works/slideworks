import { useMemo } from 'react';
import { qrMatrix } from './qrMatrix';

// A 4-module quiet zone (SW-5 J6, D14).
const QUIET_ZONE = 4;

// A QR code of text, drawn as an SVG that fills its box: pure black on white, never the deck's palette, so it scans
// in any look. It sits outside slides/** for that reason.
export function QrCode({ text, label }: { text: string; label: string }) {
  const modules = useMemo(() => qrMatrix(text), [text]);
  const size = modules.length + QUIET_ZONE * 2;
  const path = modules
    .flatMap((row, y) =>
      row.map((dark, x) => (dark ? `M${x + QUIET_ZONE} ${y + QUIET_ZONE}h1v1h-1z` : '')),
    )
    .join('');
  return (
    <svg
      data-testid="qr-code"
      role="img"
      aria-label={label}
      viewBox={`0 0 ${size} ${size}`}
      shapeRendering="crispEdges"
      style={{ width: '100%', height: '100%', display: 'block' }}
    >
      <rect width={size} height={size} fill="#ffffff" />
      <path d={path} fill="#000000" />
    </svg>
  );
}
