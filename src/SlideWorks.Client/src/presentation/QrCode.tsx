import QRCode from 'qrcode';

const QUIET_ZONE = 2;

// The SVG path of the QR code for `text` and its size in modules, quiet zone included. Specs call this to learn
// what the encoder makes for a url, so QrCode.tsx stays the one file that imports the encoder.
export function qrPath(text: string): { path: string; total: number } {
  const { modules } = QRCode.create(text, { errorCorrectionLevel: 'M' });
  const size = modules.size;
  let path = '';
  for (let row = 0; row < size; row++) {
    for (let col = 0; col < size; col++) {
      if (modules.get(row, col)) path += `M${col + QUIET_ZONE} ${row + QUIET_ZONE}h1v1h-1z`;
    }
  }
  return { path, total: size + QUIET_ZONE * 2 };
}

// A QR code for `text` as an SVG made of React nodes (no HTML strings), drawn as one path so it scales to any size.
export function QrCode({ text, label }: { text: string; label: string }) {
  const { path, total } = qrPath(text);
  return (
    <svg
      data-testid="phone-connect-qr"
      role="img"
      aria-label={label}
      viewBox={`0 0 ${total} ${total}`}
      shapeRendering="crispEdges"
      className="h-44 w-44 shrink-0 rounded-panel border border-edge bg-white"
    >
      <path d={path} fill="#101828" />
    </svg>
  );
}
