import { useEffect, useMemo, useRef } from 'react';
import type { PresenterSlide, PresenterView } from '../api/types';
import { PresenterThumb } from './PresenterThumb';

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
        heading: slide.part === null ? null : (view.parts[slide.part - 1]?.name ?? null),
        slides: [slide],
      });
  }
  return groups;
}

// The tile one row above or below the focused one, nearest across; null when there is no such row.
function verticalNeighbour(tiles: HTMLElement[], from: HTMLElement, direction: 1 | -1) {
  const origin = from.getBoundingClientRect();
  const originX = origin.left + origin.width / 2;
  let rowTop: number | null = null;
  let best: HTMLElement | null = null;
  let bestDistance = Infinity;
  for (const tile of tiles) {
    const box = tile.getBoundingClientRect();
    if ((box.top - origin.top) * direction <= 1) continue;
    if (rowTop === null || (box.top - rowTop) * direction < -1) {
      rowTop = box.top;
      best = null;
      bestDistance = Infinity;
    }
    if (Math.abs(box.top - rowTop) > 1) continue;
    const distance = Math.abs(box.left + box.width / 2 - originX);
    if (distance < bestDistance) {
      best = tile;
      bestDistance = distance;
    }
  }
  return best;
}

// The presenter window's overview grid (O): every slide of the deck, grouped by part, the current one marked; a click
// or Enter jumps there. Esc closes it, and closing twice is harmless.
export function Overview({ view, current, skipDeepDives, onJump, onClose }: Props) {
  const groups = useMemo(() => groupByPart(view), [view]);
  const root = useRef<HTMLDivElement>(null);
  const focusTarget = view.slides.some((s) => s.number === current)
    ? current
    : (view.slides[0]?.number ?? 0);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  // Focus starts on the current slide, once, when the overview opens.
  useEffect(() => {
    root.current
      ?.querySelector<HTMLElement>(`[data-testid="overview-slide-${focusTarget}"]`)
      ?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const moveFocus = (event: React.KeyboardEvent<HTMLDivElement>) => {
    const tiles = Array.from(
      root.current?.querySelectorAll<HTMLElement>('[data-testid^="overview-slide-"]') ?? [],
    );
    const at = tiles.indexOf(document.activeElement as HTMLElement);
    if (at < 0) return;
    let next: HTMLElement | null | undefined;
    if (event.key === 'ArrowRight') next = tiles[at + 1];
    else if (event.key === 'ArrowLeft') next = tiles[at - 1];
    else if (event.key === 'ArrowDown') next = verticalNeighbour(tiles, tiles[at], 1);
    else if (event.key === 'ArrowUp') next = verticalNeighbour(tiles, tiles[at], -1);
    else if (event.key === 'Home') next = tiles[0];
    else if (event.key === 'End') next = tiles[tiles.length - 1];
    else return;
    event.preventDefault();
    next?.focus();
  };

  const deepDiveCount = view.slides.filter((s) => s.deepDive).length;

  return (
    <div
      ref={root}
      data-testid="overview"
      role="dialog"
      aria-label="Overview"
      onKeyDown={moveFocus}
      className="h-full overflow-auto bg-[#0b0f19] p-4 text-[#e6e9f2]"
    >
      <div className="mb-3 flex flex-wrap items-center gap-2.5 text-[12.5px] text-[#9aa3b8]">
        <b className="text-sm font-semibold text-[#e6e9f2]">Overview</b>
        <span>
          {view.slides.length} slides · {view.parts.length} parts · {deepDiveCount} deep dives
          {skipDeepDives ? ' (skipping)' : ''}
        </span>
        <span className="flex-1" />
        <span>Esc or O to close</span>
      </div>
      {groups.map((group) => (
        <section
          key={`${group.part}-${group.slides[0].number}`}
          data-testid="overview-group"
          data-part={group.part ?? undefined}
          aria-label={group.heading ?? undefined}
        >
          {group.heading && (
            <h3
              data-testid="overview-part"
              className="mt-3.5 mb-2 text-[11px] leading-none font-semibold tracking-[0.12em] text-[#6ea0ff] uppercase first:mt-0"
            >
              {group.heading}
            </h3>
          )}
          <div className="grid grid-cols-3 items-start gap-3 min-[900px]:grid-cols-5">
            {group.slides.map((slide) => {
              const isCurrent = slide.number === current;
              const dimmed = skipDeepDives && slide.deepDive;
              return (
                <button
                  key={slide.number}
                  type="button"
                  data-testid={`overview-slide-${slide.number}`}
                  data-skipped={dimmed ? 'true' : undefined}
                  aria-current={isCurrent ? 'true' : undefined}
                  aria-label={
                    slide.headline
                      ? `Jump to slide ${slide.number}: ${slide.headline}`
                      : `Jump to slide ${slide.number}`
                  }
                  tabIndex={slide.number === focusTarget ? 0 : -1}
                  onClick={() => onJump(slide.number)}
                  className="group relative block cursor-pointer rounded-[5px] text-left focus-visible:outline-none"
                >
                  {isCurrent && (
                    <span
                      data-testid="overview-now"
                      className="absolute top-1.5 left-1.5 z-10 rounded bg-[#6ea0ff] px-1.5 py-[3px] text-[10px] leading-none font-semibold tracking-[0.06em] text-[#0b1020] uppercase"
                    >
                      Now
                    </span>
                  )}
                  <div
                    data-testid="overview-frame"
                    className={`overflow-hidden rounded outline-offset-2 group-hover:outline-2 group-hover:outline-white/35 group-focus-visible:outline-2 group-focus-visible:outline-[#6ea0ff] ${
                      isCurrent ? 'outline-3 outline-[#6ea0ff]' : ''
                    } ${dimmed ? 'opacity-30' : ''}`}
                  >
                    <PresenterThumb view={view} slide={slide.number} />
                  </div>
                  <div
                    className={`mt-[5px] flex items-center gap-1.5 text-[11.5px] leading-tight text-[#9aa3b8] ${
                      dimmed ? 'opacity-60' : ''
                    }`}
                  >
                    <span className="font-mono text-[#e6e9f2]">{slide.number}</span>
                    <span className="truncate">{slide.headline?.split('\n')[0]}</span>
                    {slide.deepDive && (
                      <span
                        data-testid="overview-deep-dive"
                        className="ml-auto flex-none rounded bg-[#7a5af8]/20 px-[5px] py-[3px] text-[10px] leading-none font-semibold tracking-[0.08em] whitespace-nowrap text-[#c4b5fd] uppercase"
                      >
                        Deep dive
                      </span>
                    )}
                  </div>
                </button>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}
