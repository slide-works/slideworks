import { Link } from 'react-router-dom';
import type { LivePresentation } from '../api/types';
import type { PresentationRole } from '../presentation/types';

const ROLE_LABELS: Record<PresentationRole, string> = {
  presenter: 'presenter view',
  audience: 'audience window',
  phone: 'phone',
};

// One live talk on the phone's pick page (P4): its title, where it is, the windows it is open in and when it started
// (text only, no thumbnail), with the way into its speaker mode.
export function LiveCard({ talk }: { talk: LivePresentation }) {
  return (
    <li
      data-testid="remote-live-card"
      data-deck-id={talk.deckId}
      className="grid gap-2 rounded-xl border border-[#1f2937] bg-[#111827] p-3"
    >
      <h2 data-testid="remote-live-title" className="text-sm font-bold">
        {talk.title}
      </h2>
      <p data-testid="remote-live-slide" className="text-xs text-[#94a3b8]">
        Slide {talk.slide} of {talk.total} · {talk.startedLabel}
      </p>
      <ul className="grid gap-0.5 text-xs text-[#94a3b8]">
        {talk.windows.map((window, index) => (
          <li key={index} data-testid="remote-live-window" data-role={window.role}>
            {window.label} · {ROLE_LABELS[window.role]}
          </li>
        ))}
      </ul>
      <Link
        data-testid="remote-live-control"
        to={`/remote/${encodeURIComponent(talk.deckId)}`}
        aria-label={`Control this talk: ${talk.title}`}
        className="rounded-[10px] bg-[#3b5bdb] p-3 text-center text-sm font-semibold text-white"
      >
        Control this talk
      </Link>
    </li>
  );
}
