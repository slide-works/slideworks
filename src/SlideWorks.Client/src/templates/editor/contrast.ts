import pairsJson from '@contracts/contrast-pairs.json' with { type: 'json' };

// WCAG 2.x contrast for the palette editor's instant feedback. It matches
// src/SlideWorks.Core/Look/Contrast.cs (the same luminance formula, the same sRGB mix) and the
// pairs are the server's own, read from src/contracts/contrast-pairs.json. The server's checklist
// still decides what can be published.

// One pair of tokens: the text colour on its ground. A pair with `over` and `share` has a ground
// that is `back` mixed at `share` over the token `over` (CSS color-mix in srgb).
export type ContrastPair = {
  name: string;
  fore: string;
  back: string;
  needed: number;
  over?: string;
  share?: number;
};

export const CONTRAST_PAIRS: readonly ContrastPair[] = pairsJson as ContrastPair[];

// The bar an extra colour's ink must reach on its colour (ExtraColours.INK_BAR on the server).
export const EXTRA_INK_NEEDED = 4.5;

const HEX = /^#[0-9a-fA-F]{6}$/;

// The one test a colour typed into the editor passes before it is saved or drawn.
export const isHexColour = (value: string): boolean => HEX.test(value);

type Rgb = [number, number, number];

const rgbOf = (hex: string): Rgb => [
  parseInt(hex.slice(1, 3), 16),
  parseInt(hex.slice(3, 5), 16),
  parseInt(hex.slice(5, 7), 16),
];

const hexOf = (rgb: Rgb): string =>
  '#' + rgb.map((channel) => channel.toString(16).padStart(2, '0')).join('');

const linear = (channel: number): number => {
  const c = channel / 255;
  return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
};

const luminance = ([r, g, b]: Rgb): number =>
  0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);

// The contrast ratio of two 6-digit hex colours, 1 to 21.
export function contrastRatio(fore: string, back: string): number {
  const a = luminance(rgbOf(fore));
  const b = luminance(rgbOf(back));
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

// The ratio as a person reads it, "3.6:1". It is cut, not rounded, to one place (as the server's
// messages are), so a pair that fails never reads as the bar it missed: 4.46 is "4.4:1".
export function ratioText(ratio: number): string {
  return `${(Math.floor(ratio * 10 + 1e-9) / 10).toFixed(1)}:1`;
}

// `fore` mixed over `back` at `share` (0 is back, 1 is fore), channel by channel, rounding halves up.
export function mixColours(fore: string, back: string, share: number): string {
  const f = rgbOf(fore);
  const b = rgbOf(back);
  return hexOf(f.map((channel, i) => Math.round(channel * share + b[i]! * (1 - share))) as Rgb);
}

export type PairCheck = {
  pair: ContrastPair;
  fore: string;
  // The ground the text sits on: the pair's `back`, or `back` mixed over `over`.
  ground: string;
  ratio: number;
  pass: boolean;
};

// Every pair against a palette's tokens. A pair naming a token that is missing or not a hex colour
// is left out: the server's own refusal covers it.
export function checkPairs(tokens: Readonly<Record<string, string>>): PairCheck[] {
  return CONTRAST_PAIRS.flatMap((pair) => {
    const fore = tokens[pair.fore];
    const back = tokens[pair.back];
    const over = pair.over === undefined ? undefined : tokens[pair.over];
    if (fore === undefined || back === undefined || !isHexColour(fore) || !isHexColour(back)) {
      return [];
    }
    let ground = back;
    if (pair.over !== undefined && pair.share !== undefined) {
      if (over === undefined || !isHexColour(over)) return [];
      ground = mixColours(back, over, pair.share);
    }
    const ratio = contrastRatio(fore, ground);
    return [{ pair, fore, ground, ratio, pass: ratio >= pair.needed }];
  });
}

const shade = (colour: string, towards: Rgb, amount: number): string =>
  hexOf(rgbOf(colour).map((c, i) => Math.round(c + (towards[i]! - c) * amount)) as Rgb);

// The colour nearest `colour` that reaches `needed` against `against`: it darkens or lightens in
// 1% steps (whichever passes first; the one with the higher ratio on a tie) and answers null when
// not even black or white passes.
export function nearestPassing(colour: string, against: string, needed: number): string | null {
  if (contrastRatio(colour, against) >= needed) return colour;
  for (let step = 1; step <= 100; step += 1) {
    const dark = shade(colour, [0, 0, 0], step / 100);
    const light = shade(colour, [255, 255, 255], step / 100);
    const darkRatio = contrastRatio(dark, against);
    const lightRatio = contrastRatio(light, against);
    if (darkRatio >= needed || lightRatio >= needed) {
      return darkRatio >= lightRatio ? dark : light;
    }
  }
  return null;
}

const distance = (a: string, b: string): number => {
  const x = rgbOf(a);
  const y = rgbOf(b);
  return Math.hypot(x[0] - y[0], x[1] - y[1], x[2] - y[2]);
};

export type Fix = { token: string; colour: string };

// The smallest change that makes a failing pair pass: the text token or, when its ground is a
// plain token, the ground token, whichever has to move less. A mixed ground is derived, so only
// the text moves. Null when nothing passes.
export function fixFor(check: PairCheck, tokens: Readonly<Record<string, string>>): Fix | null {
  const { pair, fore, ground } = check;
  const options: Fix[] = [];
  const text = nearestPassing(fore, ground, pair.needed);
  if (text !== null) options.push({ token: pair.fore, colour: text });
  const back = tokens[pair.back];
  if (pair.over === undefined && back !== undefined) {
    const fill = nearestPassing(back, fore, pair.needed);
    if (fill !== null) options.push({ token: pair.back, colour: fill });
  }
  const moved = (fix: Fix) => distance(fix.colour, tokens[fix.token] ?? fix.colour);
  return options.sort((a, b) => moved(a) - moved(b))[0] ?? null;
}
