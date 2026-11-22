import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import type { LookView } from '../api/types';
import { useFitWidth } from '../pages/deck/useFitWidth';
import { SlideRenderer, type RenderableSlide } from '../slides/SlideRenderer';
import type { LayoutTree, TreeNode } from '../slides/custom/layoutTree';
import { templateErrorCode, templatesApi } from './api';
import { BASE_PALETTE } from './editor/EditorPreview';
import { LayoutUploader } from './editor/layouts/LayoutUploader';
import { lookFromManifest } from './editor/preview/lookFromManifest';
import { gradientTextsOf, lowContrastOf } from './review/contrast';
import { overflowOf, SAFE_AREA } from './review/safeArea';
import { undrawnOf } from './review/undrawn';
import { CANVAS_WIDTH } from './review/textRuns';
import { REVIEW_TEST_IDS, reviewRenderTestId, type ReviewRender } from './review/testIds';
import { TEMPLATES_TEST_IDS } from './testIds';
import type { LayoutReviewView } from './types';
import { useTemplateDraft } from './useTemplateDraft';

const NOT_FOUND = 'This layout does not exist or is not yours to review.';
const NOT_LOADED = 'Could not load this layout.';
const CHANGED = 'This layout changed. Reload to review the new files.';

type Measure = {
  measureOverflow?: (frame: HTMLElement) => string[];
  measureContrast?: (frame: HTMLElement) => string[];
};

// What the browser found in this layout's renders: how far the maximum leaves the safe area, the
// text that is too faint on light and on dark, and how many texts sit on a gradient.
// A render that was missing or not drawn is a finding too (`undrawn`): it can never read as a pass.
type Measured = {
  overflow: string[];
  light: string[];
  dark: string[];
  gradients: number;
  undrawn: string[];
};

const FRAME = '[data-testid="slide-frame"]';
const nextFrame = () => new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

type Loaded =
  | { kind: 'loading' }
  | { kind: 'failed'; message: string }
  | { kind: 'loaded'; review: LayoutReviewView };

// /templates/:id/layouts/:name. Keyed so Retry starts the page afresh.
export function LayoutReviewPage(measure: Measure) {
  const { id = '', name = '' } = useParams();
  const [attempt, setAttempt] = useState(0);
  return (
    <Review
      key={`${id}/${name}/${attempt}`}
      id={id}
      name={name}
      retry={() => setAttempt((count) => count + 1)}
      {...measure}
    />
  );
}

const ofSurface = (review: LayoutReviewView) => review.surface !== 'light';

function Review({
  id,
  name,
  retry,
  measureOverflow = overflowOf,
  measureContrast = lowContrastOf,
}: { id: string; name: string; retry: () => void } & Measure) {
  const draft = useTemplateDraft(id);
  const [state, setState] = useState<Loaded>({ kind: 'loading' });
  const [measured, setMeasured] = useState<{ asset: string; found: Measured } | null>(null);
  const [approving, setApproving] = useState(false);
  const [approveError, setApproveError] = useState<string | null>(null);
  const statusHeading = useRef<HTMLHeadingElement>(null);
  const frames = useRef<Partial<Record<ReviewRender, HTMLDivElement | null>>>({});
  const [focusStatus, setFocusStatus] = useState(0);

  useEffect(() => {
    let active = true;
    templatesApi.layoutReview(id, name).then(
      (review) => active && setState({ kind: 'loaded', review }),
      (error: unknown) =>
        active &&
        setState({
          kind: 'failed',
          message: error instanceof ApiError && error.status === 404 ? NOT_FOUND : NOT_LOADED,
        }),
    );
    return () => {
      active = false;
    };
  }, [id, name]);

  const review = state.kind === 'loaded' ? state.review : null;
  const asset = review?.asset ?? null;
  const loadedDraft = draft.load.kind === 'loaded' ? draft.load.draft : null;
  const ready = review !== null && loadedDraft !== null;

  useEffect(() => {
    if (focusStatus > 0) statusHeading.current?.focus();
  }, [focusStatus]);

  // Measure once the fonts are in and the renders have painted twice.
  useEffect(() => {
    if (!ready || review === null) return;
    let active = true;
    const forAsset = review.asset;
    void (async () => {
      await document.fonts.ready;
      await nextFrame();
      await nextFrame();
      if (!active) return;
      const frameOf = (render: ReviewRender) =>
        frames.current[render]?.querySelector<HTMLElement>(FRAME) ?? null;
      const needDark = ofSurface(review);
      const maximum = frameOf('maximum');
      const sample = frameOf('sample');
      const dark = needDark ? frameOf('dark') : null;
      const undrawn: string[] = [];
      // A render that cannot be measured is reported, and measured as nothing.
      const drawn = (frame: HTMLElement | null, what: string): HTMLElement | null => {
        const why = undrawnOf(frame, what);
        if (why !== null) undrawn.push(why);
        return why === null ? frame : null;
      };
      const maximumFrame = drawn(maximum, 'maximum');
      const sampleFrame = drawn(sample, 'sample');
      const darkFrame = needDark ? drawn(dark, 'dark') : null;
      setMeasured({
        asset: forAsset,
        found: {
          overflow: maximumFrame === null ? [] : measureOverflow(maximumFrame),
          light: sampleFrame === null ? [] : measureContrast(sampleFrame),
          dark: darkFrame === null ? [] : measureContrast(darkFrame),
          gradients: [sampleFrame, darkFrame].reduce(
            (total, frame) => total + (frame === null ? 0 : gradientTextsOf(frame)),
            0,
          ),
          undrawn,
        },
      });
    })();
    return () => {
      active = false;
    };
    // The measurement stands in for nothing else: it reruns when the layout's files change.
  }, [ready, asset]); // eslint-disable-line react-hooks/exhaustive-deps

  const look = useMemo<LookView | null>(
    () =>
      review === null || loadedDraft === null
        ? null
        : {
            ...lookFromManifest(loadedDraft.manifest, loadedDraft.assets, BASE_PALETTE),
            customLayouts: [review.layout],
          },
    [review, loadedDraft],
  );

  const reread = useCallback(async () => {
    try {
      const fresh = await templatesApi.layoutReview(id, name);
      setState({ kind: 'loaded', review: fresh });
      return fresh;
    } catch {
      return null;
    }
  }, [id, name]);

  async function approve(current: LayoutReviewView, found: Measured) {
    setApproving(true);
    setApproveError(null);
    try {
      await templatesApi.approveLayout(id, name, {
        asset: current.asset,
        overflow: found.overflow,
        contrast: [...found.light, ...found.dark.map((line) => `On dark: ${line}`)],
      });
    } catch (error) {
      setApproveError(
        templateErrorCode(error) === 'layout_changed'
          ? CHANGED
          : `Could not approve ${name}. Try again.`,
      );
      setApproving(false);
      return;
    }
    // Approving answers 204, so the review is read again to show the new state.
    const fresh = await reread();
    if (fresh === null) setState({ kind: 'loaded', review: { ...current, approved: true } });
    setApproving(false);
    setFocusStatus((count) => count + 1);
  }

  if (state.kind === 'loading' || (state.kind === 'loaded' && draft.load.kind === 'loading')) {
    return (
      <section data-testid={TEMPLATES_TEST_IDS.layoutReviewPage} aria-busy="true">
        <p role="status" className="text-meta text-ink-muted">
          Loading layout…
        </p>
      </section>
    );
  }
  if (state.kind === 'failed' || draft.load.kind === 'failed' || review === null || look === null) {
    const message =
      state.kind === 'failed'
        ? state.message
        : draft.load.kind === 'failed' && draft.load.notFound
          ? NOT_FOUND
          : NOT_LOADED;
    return (
      <section data-testid={TEMPLATES_TEST_IDS.layoutReviewPage} className="grid gap-3">
        <p role="alert" data-testid={REVIEW_TEST_IDS.error} className="text-bad">
          {message}
        </p>
        {message === NOT_FOUND ? null : (
          <div>
            <button type="button" className="btn-ghost" onClick={retry}>
              Retry
            </button>
          </div>
        )}
      </section>
    );
  }

  const found = measured?.asset === review.asset ? measured.found : null;
  const reasons = found === null ? [] : reasonsOf(found, ofSurface(review));
  const blocked = found === null || reasons.length > 0;
  const canApprove = !blocked && !review.approved;
  const steps = Math.ceil(review.steps / 2);
  const dark = ofSurface(review);

  const renders: {
    render: ReviewRender;
    title: string;
    slots: LayoutReviewView['sample'];
    step: number;
    surface?: 'dark';
    sentence: string;
  }[] = [
    {
      render: 'sample',
      title: 'Sample',
      slots: review.sample,
      step: Number.MAX_SAFE_INTEGER,
      sentence: `Sample slide in the layout ${name}, with typical content`,
    },
    ...(dark
      ? [
          {
            render: 'dark' as const,
            title: 'Dark',
            slots: review.sample,
            step: Number.MAX_SAFE_INTEGER,
            surface: 'dark' as const,
            sentence: `Sample slide in the layout ${name} on the dark surface`,
          },
        ]
      : []),
    ...(review.steps > 0
      ? [
          {
            render: 'steps' as const,
            title: `After ${steps} of ${review.steps} clicks`,
            slots: review.sample,
            step: steps,
            sentence: `Sample slide in the layout ${name} after ${steps} of ${review.steps} clicks`,
          },
        ]
      : []),
    {
      render: 'maximum',
      title: 'Every slot at its maximum',
      slots: review.maximum,
      step: Number.MAX_SAFE_INTEGER,
      sentence: `Slide in the layout ${name} with every slot at its maximum, inside the dashed safe area`,
    },
  ];

  return (
    <section
      data-testid={TEMPLATES_TEST_IDS.layoutReviewPage}
      aria-labelledby="layout-review-name"
      className="grid gap-4"
    >
      <header className="grid gap-1">
        <Link
          to={`/templates/${encodeURIComponent(id)}/edit?tab=layouts`}
          className="text-meta font-semibold text-accent underline"
        >
          Back to Custom layouts
        </Link>
        <h1 id="layout-review-name" className="text-lg font-bold tracking-tight text-ink">
          {review.name}
        </h1>
        <h2
          ref={statusHeading}
          tabIndex={-1}
          data-testid={REVIEW_TEST_IDS.status}
          className="text-sm font-semibold text-ink"
        >
          {review.approved ? 'Approved' : 'Needs review'}
        </h2>
      </header>

      <div className="flex flex-wrap items-start gap-3">
        <button
          type="button"
          data-testid={REVIEW_TEST_IDS.approve}
          className="btn"
          aria-label={`Approve layout ${name}`}
          aria-describedby={REVIEW_TEST_IDS.approveReason}
          aria-busy={approving}
          disabled={!canApprove}
          onClick={() => found !== null && !approving && void approve(review, found)}
        >
          Approve layout
        </button>
        {loadedDraft === null ? null : (
          <LayoutUploader
            draft={loadedDraft}
            change={draft.change}
            replacing={name}
            onAccepted={() => {
              setMeasured(null);
              void reread().then(() => setFocusStatus((count) => count + 1));
            }}
          />
        )}
      </div>
      <div id={REVIEW_TEST_IDS.approveReason} data-testid={REVIEW_TEST_IDS.approveReason}>
        {review.approved ? null : found === null ? (
          <p className="text-meta text-ink-muted">Measuring the layout…</p>
        ) : (
          <Reasons found={found} dark={dark} />
        )}
        {approveError === null ? null : (
          <p role="alert" className="text-meta text-bad">
            {approveError}
          </p>
        )}
      </div>

      <Checks found={found} approved={review.approved} dark={dark} />

      <div className="grid gap-4 lg:grid-cols-2">
        {renders.map(({ render, title, slots, step, surface, sentence }) => (
          <figure key={render} className="grid gap-1">
            <figcaption className="text-meta font-semibold text-ink-muted">{title}</figcaption>
            <p className="sr-only">{sentence}</p>
            <Render
              testId={reviewRenderTestId(render)}
              register={(element) => {
                frames.current[render] = element;
              }}
              look={look}
              name={review.name}
              slots={slots}
              step={step}
              surface={surface}
              safeArea={render === 'maximum'}
            />
          </figure>
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Text title="Slots" text={JSON.stringify(review.layout.slots, null, 2)} />
        <Text title="Checked tree" text={treeText(review.layout.tree)} />
      </div>
    </section>
  );
}

const plural = (count: number, one: string, many: string) => (count === 1 ? one : many);

function reasonsOf(found: Measured, dark: boolean): string[] {
  const contrast = found.light.length + (dark ? found.dark.length : 0);
  return [
    ...(found.undrawn.length === 0
      ? []
      : [
          `${found.undrawn.length} ${plural(found.undrawn.length, 'render was', 'renders were')} not measured`,
        ]),
    ...(found.overflow.length === 0
      ? []
      : [
          `${found.overflow.length} ${plural(found.overflow.length, 'item leaves', 'items leave')} the safe area`,
        ]),
    ...(contrast === 0
      ? []
      : [`${contrast} ${plural(contrast, 'text is', 'texts are')} below the contrast bar`]),
  ];
}

function Reasons({ found, dark }: { found: Measured; dark: boolean }) {
  const reasons = reasonsOf(found, dark);
  if (reasons.length === 0) return null;
  const lists: [string, string[]][] = [
    ['Not measured', found.undrawn],
    ['Outside the safe area', found.overflow],
    ['Too faint on light', found.light],
    ...(dark ? ([['Too faint on dark', found.dark]] as [string, string[]][]) : []),
  ];
  return (
    <div className="grid gap-1">
      <p className="text-meta text-bad">
        {reasons.join('. ')}. Approve is off until they are fixed.
      </p>
      {lists.map(([title, items]) =>
        items.length === 0 ? null : (
          <div key={title}>
            <h3 className="text-meta font-semibold text-ink-muted">{title}</h3>
            <ul className="grid gap-0.5">
              {items.map((item, index) => (
                <li key={index} className="text-meta text-ink">
                  {item}
                </li>
              ))}
            </ul>
          </div>
        ),
      )}
    </div>
  );
}

type Row = { label: string; state: 'pass' | 'fail' | 'todo' };

function Checks({
  found,
  approved,
  dark,
}: {
  found: Measured | null;
  approved: boolean;
  dark: boolean;
}) {
  const measuredState = (failing: number): Row['state'] =>
    found === null ? 'todo' : failing === 0 ? 'pass' : 'fail';
  const rows: Row[] = [
    { label: 'Allowed tags', state: 'pass' },
    { label: 'Tokens only', state: 'pass' },
    { label: 'Slots declared', state: 'pass' },
    { label: 'Renders drawn', state: measuredState(found?.undrawn.length ?? 0) },
    { label: 'Safe area', state: measuredState(found?.overflow.length ?? 0) },
    { label: 'Contrast on light', state: measuredState(found?.light.length ?? 0) },
    ...(dark ? [{ label: 'Contrast on dark', state: measuredState(found?.dark.length ?? 0) }] : []),
    { label: 'Approval', state: approved ? 'pass' : 'todo' },
  ];
  const words = { pass: 'Passed', fail: 'Failed', todo: 'Not yet' };
  return (
    <div className="grid gap-1">
      <ul data-testid={REVIEW_TEST_IDS.checks} className="grid gap-1">
        {rows.map(({ label, state }) => (
          <li key={label} className="flex gap-2 text-meta text-ink">
            <span className="font-semibold">{label}</span>
            <span className={state === 'fail' ? 'text-bad' : 'text-ink-muted'}>{words[state]}</span>
          </li>
        ))}
      </ul>
      {found === null || found.gradients === 0 ? null : (
        <p data-testid={REVIEW_TEST_IDS.gradientNote} className="text-meta text-ink-muted">
          {`${found.gradients} ${plural(found.gradients, 'text sits', 'texts sit')} on a gradient; look at ${plural(found.gradients, 'it', 'them')}`}
        </p>
      )}
    </div>
  );
}

function Text({ title, text }: { title: string; text: string }) {
  return (
    <div className="grid gap-1">
      <h3 className="text-meta font-semibold text-ink-muted">{title}</h3>
      <pre className="max-h-80 overflow-auto rounded-md border border-edge p-2 text-meta whitespace-pre-wrap text-ink">
        {text}
      </pre>
    </div>
  );
}

// The checked tree as an outline of plain text: the tag, its classes and what it takes from a slot.
function treeText(tree: LayoutTree): string {
  const lines: string[] = [];
  const walk = (nodes: TreeNode[], depth: number) => {
    for (const node of nodes) {
      const parts = [
        node.tag,
        ...node.classes.map((name) => `.${name}`),
        ...(node.slot === undefined ? [] : [`slot ${node.slot}`]),
        ...(node.each === undefined ? [] : [`each ${node.each}`]),
        ...(node.if === undefined ? [] : [`if ${node.if}`]),
        ...(node.step === undefined ? [] : [`step ${node.step}`]),
        ...node.vars.map((variable) => `var ${variable.name} from ${variable.slot}`),
        ...node.variants.map((variant) => `variant ${variant.slot}`),
      ];
      lines.push(`${'  '.repeat(depth)}${parts.join(' ')}`);
      walk(node.children, depth + 1);
    }
  };
  walk(tree.root, 0);
  return lines.join('\n');
}

// One slide of the layout, picture only (hidden from assistive tech and inert), scaled to the
// column. The layout is drawn by SlideRenderer from the look that holds it.
function Render({
  testId,
  register,
  look,
  name,
  slots,
  step,
  surface,
  safeArea,
}: {
  testId: string;
  register: (element: HTMLDivElement | null) => void;
  look: LookView;
  name: string;
  slots: LayoutReviewView['sample'];
  step: number;
  surface?: 'dark';
  safeArea: boolean;
}): ReactNode {
  const { ref, scale } = useFitWidth<HTMLDivElement>(CANVAS_WIDTH);
  const slide = {
    number: 1,
    layout: name,
    part: null,
    deepDive: false,
    surface: surface ?? null,
    slots,
  } as unknown as RenderableSlide;
  return (
    <div
      ref={(element) => {
        ref.current = element;
        register(element);
      }}
      data-testid={testId}
      className="relative aspect-video overflow-hidden rounded-md border border-edge bg-panel"
    >
      <div
        aria-hidden
        inert
        className="pointer-events-none absolute top-0 left-0 origin-top-left"
        style={{ transform: `scale(${scale})`, visibility: scale > 0 ? 'visible' : 'hidden' }}
      >
        <SlideRenderer
          slide={slide}
          deck={{ title: '', author: null, parts: [], look, deepDives: [] }}
          total={1}
          step={step}
        />
        {safeArea ? (
          <div
            data-testid={REVIEW_TEST_IDS.safeAreaBox}
            className="absolute border-4 border-dashed border-bad"
            style={{
              left: SAFE_AREA.left,
              top: SAFE_AREA.top,
              width: SAFE_AREA.right - SAFE_AREA.left,
              height: SAFE_AREA.bottom - SAFE_AREA.top,
            }}
          />
        ) : null}
      </div>
    </div>
  );
}
