// Max-text generators for layout fit checks. Each returns text of exactly
// `length` characters (the pad character fills a cut-off last word), so a
// test can ask for the most a slot accepts. They return strings only; the
// spec asserts on what the layout draws.

function cycleWords(words: readonly string[], length: number, pad: string): string {
  let text = '';
  for (let i = 0; text.length < length; i++) text += (text ? ' ' : '') + words[i % words.length]!;
  return text.slice(0, length).trimEnd().padEnd(length, pad);
}

/**
 * `unit` repeated and cut to `length`. The unit is wide-letter text such as
 * 'Whom We Name WOMBAT ', ending in a space so a line can break between copies.
 */
export function wideLetters(length: number, unit: string, pad = 'M'): string {
  if (unit === '') throw new Error('wideLetters: unit must not be empty');
  let text = '';
  while (text.length < length) text += unit;
  return text.slice(0, length).trimEnd().padEnd(length, pad);
}

/** Ordinary words, wide letters first. */
export function proseFill(length: number): string {
  return cycleWords(
    ['Maximum', 'Width', 'words', 'wrap', 'across', 'the', 'whole', 'slide'],
    length,
    'm',
  );
}

/** Capitals, mostly W and M, in words a line can still break between. */
export function wideCapitals(length: number): string {
  return cycleWords(['WWWWWW', 'MAXIMUM', 'WMW', 'MMMMMM', 'WOMBAT', 'WWW'], length, 'W');
}

/** Only W, in words of 5 so a line can still break (the widest text a font draws). */
export function allW(length: number): string {
  return cycleWords(['WWWWW'], length, 'W');
}

/** `text` in capitals (a slot that is drawn in capitals measures at this width). */
export function capitals(length: number): string {
  return proseFill(length).toUpperCase();
}

/** Every word of `text` with a capital first letter, as the Title Case fit fixtures are. */
export function titleCase(text: string): string {
  return text.replace(/\b\p{L}/gu, (letter) => letter.toUpperCase());
}
