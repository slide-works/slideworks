import type { PresenterView } from '../../api/types';
import { NotesText } from '../../presentation/NotesText';

type Props = { view: PresenterView; slide: number; step: number; lastKnown: boolean };

// The current slide's notes, scrolling on their own under the slide (D6). Scrolling them never moves the slide: the
// phone has no swipe (P8). While the connection is lost they stay, marked as the last known slide's.
export function SpeakerNotes({ view, slide, step, lastKnown }: Props) {
  const shown = view.slides[slide - 1];
  const steps = shown?.steps ?? 0;
  return (
    <section
      data-testid="speaker-notes"
      data-slide={slide}
      aria-label={`Notes for slide ${slide}`}
      tabIndex={0}
      className="mx-4 min-h-0 flex-1 overflow-auto rounded-xl border border-slate-700 bg-slate-900 px-3 py-2.5 text-[15px] leading-relaxed [&_b]:text-white [&_p]:mb-1.5 [&_ul]:grid [&_ul]:gap-1"
    >
      <div className="mb-1.5 flex gap-1.5 text-[10px] font-medium tracking-widest text-slate-500 uppercase">
        <span>{lastKnown ? `Notes for slide ${slide} (last known)` : 'Notes'}</span>
        {steps > 0 && (
          <span data-testid="speaker-notes-step">
            · reveal {step} of {steps}
          </span>
        )}
      </div>
      {shown?.notes ? (
        <NotesText text={shown.notes} />
      ) : (
        <p className="text-slate-400">No notes for this slide.</p>
      )}
    </section>
  );
}
