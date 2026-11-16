import { Badge } from '../blocks/Badge';
import { Chip } from '../blocks/Chip';
import { Reveal } from '../Reveal';
import type { Card as CardSlot } from '../slots';
import styles from './Card.module.css';

type CardProps = {
  card: CardSlot;
  // The step it appears at; 0 shows it from the start.
  at: number;
  step: number;
};

// One card of the cards layout, a list item: an optional badge, a title, text
// and its chips, if it has any.
export function Card({ card, at, step }: CardProps) {
  return (
    <Reveal as="li" at={at} step={step} className={styles.card}>
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
    </Reveal>
  );
}
