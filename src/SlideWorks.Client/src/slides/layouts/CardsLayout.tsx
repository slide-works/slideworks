import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { Card } from './Card';
import styles from './CardsLayout.module.css';

// A headline over 1–3 cards in a row, each an optional badge, a title, text
// and up to four chips. With reveal 'one-by-one' card n appears at step n;
// otherwise every card shows from the start (at 0).
export function CardsLayout({ slots, step }: LayoutProps<'cards'>) {
  const { kicker, headline, cards, reveal } = slots;
  return (
    <div data-testid="layout-cards" className="sw-layout">
      <SlideHeading layout="cards" kicker={kicker} headline={headline} />
      <div data-testid="cards-body" className="sw-body">
        <ul className={styles.cards}>
          {cards.map((card, i) => (
            <Card key={i} card={card} at={reveal === 'one-by-one' ? i + 1 : 0} step={step} />
          ))}
        </ul>
      </div>
    </div>
  );
}
