import { GUEST_TEST_IDS } from '../testIds';

// The language chip in the following header and the landscape overlay (SW-883 D2): the language's short label, which
// opens the language sheet. label is its accessible name, "Read the slides in {englishName}".
export function LanguageChip({
  short,
  label,
  onOpen,
}: {
  short: string;
  label: string;
  onOpen: () => void;
}) {
  return (
    <button
      type="button"
      data-testid={GUEST_TEST_IDS.languageChip}
      aria-label={label}
      onClick={onOpen}
      className="inline-flex min-h-7 items-center rounded-full bg-white/10 px-2.5 py-1 text-[11px] font-semibold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
    >
      {short}
    </button>
  );
}
