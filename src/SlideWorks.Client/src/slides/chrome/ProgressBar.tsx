import './chrome.css';

type ProgressBarProps = {
  number: number;
  total: number;
};

// A thin accent bar along the bottom of the slide, as wide as the share of the
// deck shown so far. It is decoration: the player's own controls announce the
// position, so the bar is hidden from assistive technology.
export function ProgressBar({ number, total }: ProgressBarProps) {
  const share = total > 0 ? Math.min(number, total) / total : 0;
  return (
    <div
      className="sw-progress"
      data-testid="slide-progress"
      aria-hidden="true"
      style={{ width: `calc(var(--sw-slide-width) * ${share})` }}
    />
  );
}
