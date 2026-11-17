import { GUEST_TEST_IDS } from './testIds';

// Opens from "Guest · Privacy" in the footer: four plain lines, no legal text.
export function PrivacySheet({ onClose }: { onClose: () => void }) {
  // TODO: the real sheet with its four lines, focus handling and styling to the mockups.
  return (
    <section data-testid={GUEST_TEST_IDS.privacySheet} role="dialog" aria-label="Privacy">
      <button type="button" data-testid={GUEST_TEST_IDS.privacyClose} onClick={onClose}>
        Close privacy
      </button>
    </section>
  );
}
