import { Badge } from '../blocks/Badge';
import { Chip } from '../blocks/Chip';
import { Icon } from '../blocks/Icon';
import { iconFor } from '../blocks/icons';
import { Pin } from '../blocks/Pin';
import { Reveal } from '../Reveal';
import { panelTone, toneRule, toneStyle } from '../blocks/tone';
import type { CSSProperties } from 'react';
import type { Card as CardSlot } from '../slots';
import styles from './Card.module.css';

type CardProps = {
  card: CardSlot;
  // The step it appears at; 0 shows it from the start.
  at: number;
  step: number;
};

// One card of the cards layout, a list item: an optional icon and badge, a title, text
// its chips, if it has any, and a pin. A card with a tone is drawn on its tone's
// surface and ink (soft unless its fill says solid); without one it is the
// plain card.
export function Card({ card, at, step }: CardProps) {
  const toned =
    card.tone !== undefined
      ? {
          'data-tone': card.tone,
          'data-panel': panelTone(card.tone),
          'data-fill': card.fill ?? 'soft',
          style: {
            ...toneStyle(card.tone, card.fill),
            '--card-rule': `var(--sw-${toneRule(card.tone)})`,
          } as CSSProperties,
        }
      : {};
  // Reveal draws the list item; the card inside it fills the item and carries the tone.
  return (
    <Reveal as="li" at={at} step={step} className={styles.item}>
      <div data-testid="cards-card" className={styles.card} {...toned}>
        {card.icon !== undefined && iconFor(card.icon) !== undefined ? (
          <div data-testid="cards-icon" className={styles.icon}>
            <Icon name={card.icon} size={44} />
          </div>
        ) : null}
        {card.badge !== undefined ? <Badge text={card.badge} /> : null}
        <h3 data-testid="cards-title" className={styles.title}>
          {card.title}
        </h3>
        <p data-testid="cards-text" className={styles.text}>
          {card.text}
        </p>
        {(card.chips ?? []).length > 0 ? (
          <div data-testid="cards-chips" className={styles.chips}>
            {(card.chips ?? []).map((chip, j) => (
              <Chip key={j} text={chip} />
            ))}
          </div>
        ) : null}
        {card.pin !== undefined ? (
          <div className={styles.pinRow}>
            <Pin text={card.pin} />
          </div>
        ) : null}
      </div>
    </Reveal>
  );
}
