import { NotesText } from '../NotesText';

type Props = {
  notes: string | null;
  // The notes' font size in pixels, and the A− and A+ buttons that change it.
  size: number;
  onSmaller(): void;
  onLarger(): void;
};

// The current slide's speaker notes, with A− and A+ to change their size.
export function NotesPanel({ notes, size, onSmaller, onLarger }: Props) {
  return (
    <section
      data-testid="presenter-notes"
      aria-label="Notes"
      className="grid min-w-0 content-start gap-2"
    >
      <h2 className="flex items-center gap-2 text-[11px] font-semibold tracking-[0.12em] text-[#94a3b8] uppercase">
        Notes
        <button
          type="button"
          data-testid="presenter-notes-smaller"
          aria-label="Smaller notes"
          title="Smaller notes"
          className="ml-auto grid h-6 w-7 place-items-center rounded-md bg-white/10 text-[11px] tracking-normal text-[#e2e8f0] hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
          onClick={onSmaller}
        >
          A−
        </button>
        <button
          type="button"
          data-testid="presenter-notes-larger"
          aria-label="Larger notes"
          title="Larger notes"
          className="grid h-6 w-7 place-items-center rounded-md bg-white/10 text-[13px] tracking-normal text-[#e2e8f0] hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#8ea5ff]"
          onClick={onLarger}
        >
          A+
        </button>
      </h2>
      <div
        data-testid="presenter-notes-body"
        className="grid max-h-[50vh] min-h-36 gap-2 overflow-auto rounded-lg border border-[#1f2937] bg-[#111827] px-4 py-3 leading-normal"
        style={{ fontSize: `${size}px` }}
      >
        {notes ? (
          <NotesText text={notes} />
        ) : (
          <p className="text-[#64748b] italic">No notes for this slide.</p>
        )}
      </div>
    </section>
  );
}
