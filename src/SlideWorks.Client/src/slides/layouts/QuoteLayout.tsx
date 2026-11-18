import { useRef } from 'react';
import { Emphasised } from '../blocks/Emphasised';
import { Kicker } from '../blocks/Kicker';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { useFitText, type FitBudget } from '../useFitText';
import styles from './QuoteLayout.module.css';

// The height of the content box, from y 150 to y 996.
const CONTENT_HEIGHT = 846;

// What the quote does not take: every other box in the layout, with its margins,
// from the quote up to the layout's root. The figure of a slide with only today's
// slots centres itself with auto margins, which are free space and not counted;
// the body's own top margin is.
function quoteHeight(quote: HTMLElement): number {
  let others = 0;
  let node: HTMLElement = quote;
  while (!node.classList.contains('sw-layout') && node.parentElement !== null) {
    const parent: HTMLElement = node.parentElement;
    for (const sibling of Array.from(parent.children)) {
      if (sibling === node || !(sibling instanceof HTMLElement)) continue;
      others += outerHeight(sibling);
    }
    if (node.classList.contains('sw-body')) others += margins(node);
    node = parent;
  }
  return CONTENT_HEIGHT - others;
}

function margins(element: HTMLElement): number {
  const style = getComputedStyle(element);
  return (parseFloat(style.marginTop) || 0) + (parseFloat(style.marginBottom) || 0);
}

function outerHeight(element: HTMLElement): number {
  return element.offsetHeight + margins(element);
}

// The quote is set at 62 px in at most seven lines: 280 characters of prose fit
// at that size. Capitals or wide letters run longer, so the quote then shrinks
// until it fits seven lines and the height left by what is around it, never
// below 36 px.
const QUOTE_FIT: FitBudget = {
  start: 62,
  floor: 36,
  lines: 7,
  lineHeight: 1.22,
  maxHeight: quoteHeight,
};

// A quotation with its attribution. The slot holds the words only, so the
// layout adds the quotation marks. It opens with the Kicker block alone, or,
// with a headline, with SlideHeading and the figure in the body. A role follows
// the name; a stat sits beside the attribution; a follow-up line closes the
// figure and a source sits under it. With reveal 'one-by-one' the stat arrives
// at step 1 and the follow-up after it; both keep their space until then.
export function QuoteLayout({ slots, step }: LayoutProps<'quote'>) {
  const { kicker, headline, quote, attribution, role, stat, follow_up, source, reveal } = slots;
  const quoteRef = useRef<HTMLQuoteElement>(null);
  const spans = emphasisFor(slots, 'quote');
  useFitText(
    quoteRef,
    QUOTE_FIT,
    [quote, headline, attribution, role, stat?.value, stat?.caption, follow_up, source].join('\n'),
  );
  const words = `“${quote}”`;
  const quoteBlock = (
    <blockquote ref={quoteRef} data-testid="quote-text" className={styles.quote}>
      {spans.length > 0 ? (
        <>
          {'“'}
          <Emphasised text={quote} spans={spans} />
          {'”'}
        </>
      ) : (
        words
      )}
    </blockquote>
  );
  const extras =
    headline !== undefined ||
    role !== undefined ||
    stat !== undefined ||
    follow_up !== undefined ||
    source !== undefined;
  if (!extras) {
    return (
      <div data-testid="layout-quote" className="sw-layout">
        <Kicker testId="quote-kicker" text={kicker} />
        <figure className={styles.figure}>
          {quoteBlock}
          <figcaption data-testid="quote-attribution" className={styles.attribution}>
            {attribution}
          </figcaption>
        </figure>
      </div>
    );
  }

  const stepped = reveal === 'one-by-one';
  const statStep = stepped && stat !== undefined ? 1 : 0;
  const followUpStep = stepped && follow_up !== undefined ? statStep + 1 : 0;
  const statPanel =
    stat !== undefined ? (
      <div data-testid="quote-stat" className={styles.stat}>
        <span data-testid="quote-stat-value" className={styles.statValue}>
          {stat.value}
        </span>
        <span data-testid="quote-stat-caption" className={styles.statCaption}>
          {stat.caption}
        </span>
      </div>
    ) : null;
  const followUp =
    follow_up !== undefined ? (
      <p data-testid="quote-follow-up" className={styles.followUp}>
        {follow_up}
      </p>
    ) : null;
  return (
    <div data-testid="layout-quote" className="sw-layout">
      {headline !== undefined ? (
        <SlideHeading
          layout="quote"
          kicker={kicker}
          headline={headline}
          emphasis={emphasisFor(slots, 'headline')}
        />
      ) : (
        <Kicker testId="quote-kicker" text={kicker} />
      )}
      <div className={`sw-body ${styles.body}`}>
        <figure className={styles.figureInBody}>
          {quoteBlock}
          <figcaption data-testid="quote-attribution" className={styles.attributionRow}>
            <span data-testid="quote-who" className={styles.who}>
              {role !== undefined ? (
                <>
                  <b data-testid="quote-name" className={styles.name}>
                    {attribution}
                  </b>
                  {`, ${role}`}
                </>
              ) : (
                attribution
              )}
            </span>
            {statStep > 0 ? (
              <Reveal at={statStep} step={step} className={styles.statSlot}>
                {statPanel}
              </Reveal>
            ) : (
              statPanel
            )}
          </figcaption>
          {followUpStep > 0 ? (
            <Reveal at={followUpStep} step={step} className={styles.followUpBox}>
              {followUp}
            </Reveal>
          ) : (
            followUp !== null && <div className={styles.followUpBox}>{followUp}</div>
          )}
        </figure>
        {source !== undefined ? (
          <p data-testid="quote-source" className={styles.source}>
            {source}
          </p>
        ) : null}
      </div>
    </div>
  );
}
