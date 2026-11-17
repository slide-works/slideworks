import QRCode from 'qrcode';

// Error correction level M (SW-5 J6).
// The QR code's modules, row by row (true is dark), without the quiet zone.
export function qrMatrix(text: string): boolean[][] {
  const code = QRCode.create(text, { errorCorrectionLevel: 'M' });
  const size = code.modules.size;
  return Array.from({ length: size }, (_, row) =>
    Array.from({ length: size }, (_, column) => code.modules.get(row, column) === 1),
  );
}
