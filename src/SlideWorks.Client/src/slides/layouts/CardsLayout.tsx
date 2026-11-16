import type { ReactNode } from 'react';
import { Badge } from '../blocks/Badge';
import { Chip } from '../blocks/Chip';
import { SlideHeading } from '../blocks/SlideHeading';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './CardsLayout.module.css';

// A headline over 1–3 cards in a row, each an optional badge, a title, text
// and up to four chips. With reveal 'one-by-one' card n appears at step n.
export function CardsLayout({ slots, step }: LayoutProps<'cards'>) {
  const { kicker, headline, cards, reveal } = slots;
  return (
    <div data-testid="layout-cards" className="sw-layout">
      <SlideHeading layout="cards" kicker={kicker} headline={headline} />
      <div data-testid="cards-body" className="sw-body">
        <ul className={styles.cards}>
          {cards.map((card, i) => (
            <Item key={i} at={reveal === 'one-by-one' ? i + 1 : null} step={step}>
              {card.badge !== undefined ? <Badge text={card.badge} /> : null}
              <h3 data-testid="cards-title" className={styles.title}>
                {card.title}
              </h3>
              <p data-testid="cards-text" className={styles.text}>
                {card.text}
              </p>
              {card.chips.length > 0 ? (
                <div data-testid="cards-chips" className={styles.chips}>
                  {card.chips.map((chip, j) => (
                    <Chip key={j} text={chip} />
                  ))}
                </div>
              ) : null}
            </Item>
          ))}
        </ul>
      </div>
    </div>
  );
}

// One card, a list item: revealed at its step, or shown from the start when
// `at` is null.
function Item({ at, step, children }: { at: number | null; step: number; children: ReactNode }) {
  return at === null ? (
    <li className={styles.card}>{children}</li>
  ) : (
    <Reveal as="li" at={at} step={step} className={styles.card}>
      {children}
    </Reveal>
  );
}
