import type { LookFont } from '../api/look';

// The faces a page has added to document.fonts, keyed by family, weight, style and url, each with the promise that
// settles when its file has loaded or failed. A page of forty frames adds each file once. A face that failed is
// dropped from document.fonts, so the text falls back to the built-in stack, and its key stays here so the file
// is not fetched again.
const faces = new Map<string, Promise<void>>();

const keyOf = (font: LookFont) => `${font.family}|${font.weight}|${font.style}|${font.url}`;

function addFace(font: LookFont): Promise<void> {
  const key = keyOf(font);
  const known = faces.get(key);
  if (known) return known;
  let settled: Promise<void>;
  try {
    const face = new FontFace(font.family, `url(${JSON.stringify(font.url)})`, {
      weight: String(font.weight),
      style: font.style,
    });
    document.fonts.add(face);
    settled = face.load().then(
      () => undefined,
      () => {
        document.fonts.delete(face);
      },
    );
  } catch {
    settled = Promise.resolve();
  }
  faces.set(key, settled);
  return settled;
}

// Adds a face for each font that this page has not added yet, and resolves once each has loaded or failed. Never rejects.
export function addFontFaces(fonts: readonly LookFont[]): Promise<void> {
  return Promise.all(fonts.map(addFace)).then(() => undefined);
}

// Settles when every face of the look has loaded or failed, then when the document's fonts are ready. A render waits
// on it before it signals ready. It never rejects: a face that fails does not hold the render up.
export async function fontsSettled(look?: { fonts: readonly LookFont[] }): Promise<void> {
  const fonts = look?.fonts ?? [];
  await addFontFaces(fonts);
  await Promise.all(
    fonts.map((font) =>
      document.fonts
        .load(`${font.style} ${font.weight} 16px ${JSON.stringify(font.family)}`)
        .catch(() => undefined),
    ),
  );
  await document.fonts.ready.catch(() => undefined);
}
