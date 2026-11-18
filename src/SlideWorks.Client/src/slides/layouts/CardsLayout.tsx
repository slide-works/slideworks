import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import { Card } from './Card';
import { cardListAttrs } from './cardScale';
import styles from './CardsLayout.module.css';

// A headline over 1-4 cards, each an optional icon, badge, title, text, up to
// four chips and a pin. Up to three cards stand in a row, four lay out 2x2, and
// the list fills the body (less the callout, if any), so every card is as tall
// as its row. With reveal 'one-by-one' card n appears at step n; otherwise
// every card shows from the start (at 0). The callout, if there is one, sits
// under the cards, last when its reveal says so.
export function CardsLayout({ slots, step }: LayoutProps<'cards'>) {
  const { kicker, headline, cards, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  return (
    <div data-testid="layout-cards" className="sw-layout">
      <SlideHeading
        layout="cards"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="cards-body" className={`sw-body ${styles.body}`}>
        <ul className={styles.cards} {...cardListAttrs(cards)}>
          {cards.map((card, i) => (
            <Card key={i} card={card} at={oneByOne ? i + 1 : 0} step={step} />
          ))}
        </ul>
        {callout !== undefined ? (
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? cards.length : 0, callout)}
          />
        ) : null}
      </div>
    </div>
  );
}
