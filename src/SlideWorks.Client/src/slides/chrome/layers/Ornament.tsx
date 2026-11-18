import { useSlideLook } from '../lookContext';

// The palette tokens a dot may be coloured with. A look names them as strings,
// so a name is checked against this list before it reaches a style value.
const DOT_TOKENS: ReadonlySet<string> = new Set([
  'accent',
  'accent-2',
  'accent-glow',
  'good',
  'warn',
  'bad',
  'extra-1',
  'extra-2',
  'extra-3',
  'extra-4',
]);

const MAX_DOTS = 6;

// A look layer of the slide frame (T8): the template's ornament. The title
// slide's dots sit in its own centred stack, so they are drawn by OrnamentDots,
// which TitleLayout renders; the frame-level layer has nothing to draw.
export function Ornament() {
  return null;
}

// The dots above the title slide's kicker. With no ornament colours in the look
// they are today's three dots, drawn by the layout's own CSS; otherwise one fully
// opaque dot per known token name, at most six.
export function OrnamentDots() {
  const { ornament } = useSlideLook();
  const names = ornament.colours.filter((name: string) => DOT_TOKENS.has(name)).slice(0, MAX_DOTS);
  if (ornament.colours.length === 0) {
    return (
      <>
        <i />
        <i />
        <i />
      </>
    );
  }
  return (
    <span style={{ display: 'contents' }} data-testid="title-ornament" aria-hidden="true">
      {names.map((name, index) => (
        <i key={`${name}-${index}`} style={{ background: `var(--sw-${name})`, opacity: 1 }} />
      ))}
    </span>
  );
}
