import { Link } from 'react-router-dom';

// "▶ Present": opens the deck in the player.
export function PresentLink({ playUrl }: { playUrl: string }) {
  return (
    <Link
      to={playUrl}
      data-testid="deck-present"
      className="inline-flex items-center gap-1.5 rounded-md border border-edge-strong bg-panel px-3 py-1 text-meta font-medium whitespace-nowrap text-ink no-underline hover:border-ink-faint"
    >
      ▶ Present
    </Link>
  );
}
