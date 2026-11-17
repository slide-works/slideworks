import { useRef, useState, type ReactNode } from 'react';
import { PrivacySheet } from './PrivacySheet';
import { GUEST_TEST_IDS } from './testIds';

// The night surface every guest screen sits on, with the "Guest · no account" footer and the privacy sheet it opens.
// The footer is left out in landscape, where the slide takes the whole screen. fill pins the shell to the screen's height
// in landscape so the slide can be sized from it.
export function GuestShell({ fill = false, children }: { fill?: boolean; children: ReactNode }) {
  const [privacyOpen, setPrivacyOpen] = useState(false);
  const privacyButton = useRef<HTMLButtonElement>(null);

  const closePrivacy = () => {
    setPrivacyOpen(false);
    privacyButton.current?.focus();
  };

  return (
    <div
      className={`flex min-h-dvh flex-col [background:var(--sw-night-surface)] text-(--sw-night-ink) ${fill ? '[@media(orientation:landscape)_and_(max-height:500px)]:h-dvh [@media(orientation:landscape)_and_(max-height:500px)]:min-h-0' : ''}`}
    >
      {children}
      <footer className="flex items-center justify-between px-5 text-xs text-(--sw-night-muted) [@media(orientation:landscape)_and_(max-height:500px)]:hidden">
        <span>Guest · no account</span>
        <button
          ref={privacyButton}
          type="button"
          aria-label="Privacy: what this page keeps about you as a guest"
          data-testid={GUEST_TEST_IDS.privacyOpen}
          onClick={() => setPrivacyOpen(true)}
          className="min-h-12 px-2 font-semibold text-(--sw-accent-glow) focus-visible:outline-2 focus-visible:outline-(--sw-accent-glow)"
        >
          Privacy
        </button>
      </footer>
      {privacyOpen && <PrivacySheet onClose={closePrivacy} />}
    </div>
  );
}
