import { GUEST_TEST_IDS } from '../testIds';

// The one-time line that tells a guest the slides started in their phone's language (D3), with Change to open the sheet.
export function LanguageBanner({ name, onChange }: { name: string; onChange: () => void }) {
  return (
    <div
      data-testid={GUEST_TEST_IDS.languageBanner}
      role="status"
      className="flex items-center gap-2 rounded-xl border border-(--sw-night-line) bg-white/5 px-3 py-2 text-xs text-(--sw-night-2) [@media(orientation:landscape)_and_(max-height:500px)]:hidden"
    >
      <span className="min-w-0 flex-1">{`Showing the slides in ${name}.`}</span>
      <button
        type="button"
        data-testid={GUEST_TEST_IDS.languageBannerChange}
        onClick={onChange}
        className="min-h-8 rounded-lg px-2 font-semibold text-(--sw-accent-glow) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
      >
        Change
      </button>
    </div>
  );
}
