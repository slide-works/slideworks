// The fields of the Basics tab, and which one a refusal's manifest path is about.
export type BasicsField =
  | 'label'
  | 'name'
  | 'purpose'
  | 'layouts'
  | 'tracker'
  | 'progress'
  | 'deepDivesOnDivider'
  | 'draft'
  | 'topBar'
  | 'footer'
  | 'outline'
  | 'houseStyle'
  | 'shape';

const TOP_LEVEL: readonly BasicsField[] = [
  'label',
  'name',
  'purpose',
  'layouts',
  'outline',
  'houseStyle',
  'shape',
];
const CHROME: readonly BasicsField[] = [
  'tracker',
  'progress',
  'deepDivesOnDivider',
  'draft',
  'topBar',
  'footer',
];

// `purpose`, `outline[2]`, `chrome.topBar`, `shape.radius.md`: the field it is about, or null
// when the path is none of this tab's (fonts, palette, a missing path).
export function fieldOfPath(path: string | undefined): BasicsField | null {
  if (!path) return null;
  const [head, second] = path.split(/[.[]/);
  if (head === 'chrome') return CHROME.find((field) => field === second?.replace(']', '')) ?? null;
  return TOP_LEVEL.find((field) => field === head) ?? null;
}

// One line each: blank lines go, and each line is trimmed.
export const linesOf = (text: string): string[] =>
  text
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '');

// `object` without `key`, as a copy.
export function omit<T extends object>(object: T, key: keyof T): T {
  const copy = { ...object };
  delete copy[key];
  return copy;
}
