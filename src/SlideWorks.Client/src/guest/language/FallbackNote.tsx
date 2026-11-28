import { GUEST_TEST_IDS } from '../testIds';

// "N lines on this slide are in English": the slide's lines that have no translation yet and show in the primary
// language. lines is the state's language.fallbackLines as it comes (untranslated emphasis is already left out, D24).
// Hidden at 0.
export function FallbackNote({ lines, primary }: { lines: number; primary: string }) {
  if (lines <= 0) return null;
  return (
    <p
      data-testid={GUEST_TEST_IDS.fallbackNote}
      role="status"
      className="text-[11px] text-(--sw-night-muted) [@media(orientation:landscape)_and_(max-height:500px)]:hidden"
    >
      {lines === 1
        ? `1 line on this slide is in ${primary}`
        : `${lines} lines on this slide are in ${primary}`}
    </p>
  );
}
