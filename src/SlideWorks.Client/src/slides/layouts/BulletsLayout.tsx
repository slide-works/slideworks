import { Callout } from '../blocks/Callout';
import { LeadText } from '../blocks/LeadText';
import { SlideHeading } from '../blocks/SlideHeading';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './BulletsLayout.module.css';

// A headline over 1–8 points, each plain text or a bold lead plus text, with
// an optional callout under them. With reveal 'one-by-one' point n appears at
// step n; the callout shows from the start.
export function BulletsLayout({ slots, step }: LayoutProps<'bullets'>) {
  const { kicker, headline, items, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  return (
    <div data-testid="layout-bullets" className="sw-layout">
      <SlideHeading layout="bullets" kicker={kicker} headline={headline} />
      <div data-testid="bullets-body" className="sw-body">
        <ul className={styles.items}>
          {items.map((item, i) => {
            const text = (
              <span data-testid="bullets-item">
                <LeadText item={item} />
              </span>
            );
            return oneByOne ? (
              <Reveal key={i} as="li" at={i + 1} step={step} className={styles.item}>
                {text}
              </Reveal>
            ) : (
              <li key={i} className={styles.item}>
                {text}
              </li>
            );
          })}
        </ul>
        {callout !== undefined ? (
          <div className={styles.callout}>
            <Callout tone={callout.tone} text={callout.text} />
          </div>
        ) : null}
      </div>
    </div>
  );
}
