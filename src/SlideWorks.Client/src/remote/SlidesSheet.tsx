import { useEffect, useMemo, useRef } from 'react';
import type { PresenterSlide, PresenterView } from '../api/types';
import { PresenterThumb } from '../presentation/PresenterThumb';

type Props = {
  view: PresenterView;
  current: number;
  skipDeepDives: boolean;
  onJump(n: number): void;
  onClose(): void;
};

type Group = { part: number | null; heading: string | null; slides: PresenterSlide[] };

// Consecutive slides of the same part form one group, in deck order. A slide with no part has no heading.
function groupByPart(view: PresenterView): Group[] {
  const groups: Group[] = [];
  for (const slide of view.slides) {
    const last = groups[groups.length - 1];
    if (last && last.part === slide.part) last.slides.push(slide);
    else
      groups.push({
        part: slide.part,
        heading: slide.part === null ? null : (view.parts[slide.part - 1] ?? null),
        slides: [slide],
      });
  }
  return groups;
}

// The phone's slides sheet (section 06): every slide of the deck as a row, grouped by part, the current one
// highlighted and scrolled into view. A tap jumps there and closes the sheet. Deep dives are labelled, and dimmed
// while skipping. Focus opens on the current row, or on Done when the current slide has no row.
export function SlidesSheet({ view, current, skipDeepDives, onJump, onClose }: Props) {
  const groups = useMemo(() => groupByPart(view), [view]);
  const root = useRef<HTMLDivElement>(null);

  // Focus and scroll start on the current row, once, when the sheet opens. Scrolling is instant, so no motion.
  useEffect(() => {
    const row = root.current?.querySelector<HTMLElement>(
      `[data-testid="slides-sheet-row-${current}"]`,
    );
    const target =
      row ?? root.current?.querySelector<HTMLElement>('[data-testid="slides-sheet-done"]') ?? null;
    target?.focus({ preventScroll: true });
    row?.scrollIntoView({ block: 'center' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div
      ref={root}
      data-testid="slides-sheet"
      role="dialog"
      aria-label="Slides"
      onKeyDown={(event) => {
        if (event.key === 'Escape') onClose();
      }}
      className="flex h-full flex-col overflow-auto bg-[#0b0f19] text-[#e6e9f2]"
    >
      <div className="sticky top-0 z-10 flex items-center gap-2 bg-[#0b0f19] px-3.5 pt-8 pb-2">
        <b className="text-[15px] font-semibold">Slides</b>
        <span className="flex-1" />
        <button
          type="button"
          data-testid="slides-sheet-done"
          aria-label="Close slides"
          onClick={onClose}
          className="rounded px-1.5 py-1 text-[12px] text-[#6ea0ff] focus-visible:outline-2 focus-visible:outline-[#6ea0ff]"
        >
          Done
        </button>
      </div>
      <div className="px-2.5 pb-3.5">
        {groups.map((group) => (
          <section
            key={`${group.part}-${group.slides[0].number}`}
            data-testid="slides-sheet-group"
            data-part={group.part ?? undefined}
          >
            {group.heading && (
              <h3 className="mx-1 mt-3 mb-1.5 text-[10px] leading-none font-semibold tracking-[0.12em] text-[#6ea0ff] uppercase">
                {group.heading}
              </h3>
            )}
            {group.slides.map((slide) => {
              const isCurrent = slide.number === current;
              const dimmed = skipDeepDives && slide.deepDive;
              const name = slide.headline
                ? `Jump to slide ${slide.number}: ${slide.headline}`
                : `Jump to slide ${slide.number}`;
              return (
                <button
                  key={slide.number}
                  type="button"
                  data-testid={`slides-sheet-row-${slide.number}`}
                  aria-label={name}
                  aria-current={isCurrent ? 'true' : undefined}
                  onClick={() => {
                    onJump(slide.number);
                    onClose();
                  }}
                  className={`grid w-full grid-cols-[22px_72px_minmax(0,1fr)] items-center gap-2 rounded-lg px-1 py-1.5 text-left hover:bg-white/5 focus-visible:outline-2 focus-visible:outline-[#6ea0ff] motion-safe:transition-colors ${
                    isCurrent ? 'bg-[rgb(142_165_255/0.16)]' : ''
                  } ${dimmed ? 'opacity-50' : ''}`}
                >
                  <span className="text-right font-mono text-[11.5px] text-[#9aa3b8]">
                    {slide.number}
                  </span>
                  <span className="block overflow-hidden rounded-sm">
                    <PresenterThumb view={view} slide={slide.number} />
                  </span>
                  <span className="min-w-0 text-[12.5px] leading-tight text-[#e6e9f2]">
                    <span className="line-clamp-2 break-words">{slide.headline ?? ''}</span>
                    {slide.deepDive && (
                      <span
                        data-testid="slides-sheet-deep-dive"
                        className="mt-0.5 block text-[10px] font-semibold tracking-[0.06em] text-[#c4b5fd] uppercase"
                      >
                        {skipDeepDives ? 'Deep dive · skipping' : 'Deep dive'}
                      </span>
                    )}
                  </span>
                </button>
              );
            })}
          </section>
        ))}
      </div>
    </div>
  );
}
