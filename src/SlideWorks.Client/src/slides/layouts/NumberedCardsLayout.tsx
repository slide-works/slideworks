import { SlideHeading } from '../blocks/SlideHeading';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './NumberedCardsLayout.module.css';

// A headline over 2–4 numbered cards in a row, each a title and text. With
// reveal 'one-by-one' card n appears at step n; otherwise every card shows
// from the start (at 0).
export function NumberedCardsLayout({ slots, step }: LayoutProps<'numbered-cards'>) {
  const { kicker, headline, cards, reveal } = slots;
  return (
    <div data-testid="layout-numbered-cards" className="sw-layout">
      <SlideHeading layout="numbered-cards" kicker={kicker} headline={headline} />
      <div data-testid="numbered-cards-body" className="sw-body">
        <ol className={styles.cards}>
          {cards.map((card, i) => (
            <Reveal
              key={i}
              as="li"
              at={reveal === 'one-by-one' ? i + 1 : 0}
              step={step}
              className={styles.card}
            >
              <span data-testid="numbered-cards-number" className={styles.number} aria-hidden>
                {i + 1}
              </span>
              <h3 data-testid="numbered-cards-title" className={styles.title}>
                {card.title}
              </h3>
              <p data-testid="numbered-cards-text" className={styles.text}>
                {card.text}
              </p>
            </Reveal>
          ))}
        </ol>
      </div>
    </div>
  );
}
