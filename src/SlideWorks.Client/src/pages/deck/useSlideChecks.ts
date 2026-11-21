import { useEffect, useMemo, useState, type RefObject } from 'react';
import type { DeckPageView, FindingSeverity, SlideFinding } from '../../api/types';
import { RULE_ORDER } from '../../checks/types';
import { fontsSettled } from '../../look/fontsSettled';

export type Measure = (frame: HTMLElement, slide: number) => SlideFinding[];

const SEVERITY_ORDER: readonly FindingSeverity[] = ['error', 'warning', 'note'];

// The findings of the deck's slides, by slide number: the stored ones and the measured ones together, each slide's
// list sorted errors first, then warnings, then notes, and within a severity in rule order.
export function mergeFindings(
  stored: readonly SlideFinding[],
  measured: readonly SlideFinding[],
): Map<number, SlideFinding[]> {
  const bySlide = new Map<number, SlideFinding[]>();
  for (const finding of [...stored, ...measured]) {
    const list = bySlide.get(finding.slide);
    if (list) list.push(finding);
    else bySlide.set(finding.slide, [finding]);
  }
  const rank = (finding: SlideFinding) =>
    SEVERITY_ORDER.indexOf(finding.severity) * RULE_ORDER.length + RULE_ORDER.indexOf(finding.rule);
  for (const list of bySlide.values()) list.sort((a, b) => rank(a) - rank(b));
  return bySlide;
}

const nextFrame = () => new Promise<void>((done) => requestAnimationFrame(() => done()));

// A thumbnail's frame sits in an inert div that stays hidden until its scale is known.
const isShown = (frame: HTMLElement) =>
  getComputedStyle(frame.closest('[inert]') ?? frame).visibility !== 'hidden';

export type SlideChecks = {
  bySlide: Map<number, SlideFinding[]>;
  // The slides whose measuring threw: their measured findings are missing.
  unchecked: ReadonlySet<number>;
};

// The deck page's findings: the stored ones from its own read, plus the ones measured on each thumbnail's slide
// frame once the fonts have settled. It measures again when the deck's revision or look changes.
export function useSlideChecks(
  root: RefObject<HTMLElement | null>,
  view: DeckPageView | null,
  measure: Measure,
): SlideChecks {
  const [measured, setMeasured] = useState<{
    key: string;
    findings: SlideFinding[];
    unchecked: Set<number>;
  }>({ key: '', findings: [], unchecked: new Set() });
  const slideNumbers = view?.slides.map((slide) => slide.number).join(',');
  const key = view
    ? `${view.id}|${view.revision}|${JSON.stringify(view.look)}|${slideNumbers}`
    : '';

  useEffect(() => {
    if (!view) return;
    let current = true;
    void (async () => {
      await fontsSettled(view.look);
      await nextFrame();
      await nextFrame();
      const findings: SlideFinding[] = [];
      const unchecked = new Set<number>();
      for (const slide of view.slides) {
        const selector = `[data-testid="deck-thumb"][data-slide="${slide.number}"] [data-testid="slide-frame"]`;
        let frame = root.current?.querySelector<HTMLElement>(selector) ?? null;
        // Wait (for a bounded time) for the thumbnail to be scaled and shown.
        for (let waited = 0; current && frame && !isShown(frame) && waited < 120; waited++) {
          await nextFrame();
          frame = root.current?.querySelector<HTMLElement>(selector) ?? null;
        }
        if (!current) return;
        if (!frame || !isShown(frame)) continue;
        try {
          findings.push(...measure(frame, slide.number));
        } catch (error) {
          // One slide that cannot be measured must not hide the others' findings.
          console.error(`Could not measure slide ${slide.number}`, error);
          unchecked.add(slide.number);
        }
      }
      setMeasured({ key, findings, unchecked });
    })();
    return () => {
      current = false;
    };
    // view.slides and view.look are covered by key.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, root, measure]);

  return useMemo(() => {
    const current = measured.key === key;
    return {
      bySlide: mergeFindings(view?.findings ?? [], current ? measured.findings : []),
      unchecked: current ? measured.unchecked : new Set<number>(),
    };
  }, [view?.findings, measured, key]);
}
