import type { ReactNode } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './NumberedCardsLayout.module.css';

// A headline over 2–4 numbered cards in a row, each a title and text. With
// reveal 'one-by-one' card n appears at step n.
export function NumberedCardsLayout({ slots, step }: LayoutProps<'numbered-cards'>) {
  const { kicker, headline, cards, reveal } = slots;
  return (
    <div data-testid="layout-numbered-cards" className="sw-layout">
      <SlideHeading layout="numbered-cards" kicker={kicker} headline={headline} />
      <div data-testid="numbered-cards-body" className="sw-body">
        <ol className={styles.cards}>
          {cards.map((card, i) => (
            <Item key={i} at={reveal === 'one-by-one' ? i + 1 : null} step={step}>
              <span data-testid="numbered-cards-number" className={styles.number} aria-hidden>
                {i + 1}
              </span>
              <h3 data-testid="numbered-cards-title" className={styles.title}>
                {card.title}
              </h3>
              <p data-testid="numbered-cards-text" className={styles.text}>
                {card.text}
              </p>
            </Item>
          ))}
        </ol>
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
