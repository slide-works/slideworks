import { LaterButton } from './LaterButton';

// What later Features add to the deck page.
export function LaterOnThisPage() {
  return (
    <section className="panel grid gap-3 p-4">
      <h3 className="text-xs font-semibold tracking-wide text-ink-muted uppercase">
        Later on this page
      </h3>
      <div className="flex flex-wrap gap-2">
        <LaterButton label="Comments" feature="SW-14" />
        <LaterButton label="Versions" feature="SW-14" />
        <LaterButton label="Edit text" feature="SW-13" />
      </div>
    </section>
  );
}
