import { Callout } from '../blocks/Callout';
import { Reveal } from '../Reveal';
import type { BulletItem, LayoutProps } from '../slots';
import styles from './BulletsLayout.module.css';

// A headline over 1–8 points, each plain text or a bold lead plus text, with
// an optional callout under them. With reveal 'one-by-one' point n appears at
// step n; the callout shows from the start.
export function BulletsLayout({ slots, step }: LayoutProps<'bullets'>) {
  const { kicker, headline, items, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  return (
    <div data-testid="layout-bullets" className="sw-layout">
      {kicker !== undefined ? (
        <p data-testid="bullets-kicker" className={styles.kicker}>
          {kicker}
        </p>
      ) : null}
      <h2 data-testid="bullets-headline" className={styles.headline}>
        {headline}
      </h2>
      <div data-testid="bullets-body" className="sw-body">
        <ul className={styles.items}>
          {items.map((item, i) =>
            oneByOne ? (
              <Reveal key={i} as="li" at={i + 1} step={step} className={styles.item}>
                <ItemText item={item} />
              </Reveal>
            ) : (
              <li key={i} className={styles.item}>
                <ItemText item={item} />
              </li>
            ),
          )}
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

function ItemText({ item }: { item: BulletItem }) {
  return (
    <span data-testid="bullets-item">
      {typeof item === 'string' ? (
        item
      ) : (
        <>
          <b className={styles.lead}>{item.lead}</b> {item.text}
        </>
      )}
    </span>
  );
}
