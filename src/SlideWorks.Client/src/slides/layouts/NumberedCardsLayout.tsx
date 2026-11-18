import type { CSSProperties } from 'react';
import { BodyCallout } from '../blocks/BodyCallout';
import { Pin } from '../blocks/Pin';
import { SlideHeading } from '../blocks/SlideHeading';
import { panelTone, toneRule, toneStyle } from '../blocks/tone';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps, PanelFill, Tone } from '../slots';
import { calloutStep } from '../steps';
import { cardListAttrs } from './cardScale';
import styles from './NumberedCardsLayout.module.css';

// A headline over 2–4 numbered cards, each a title and text, that fill the
// body's height. Two or three cards share three columns, four keep four. A
// card with a tone is highlighted (a fill, or with fill 'plain' a rule on top),
// and may carry a pin. With reveal 'one-by-one' card n appears at step n;
// otherwise every card shows from the start (at 0). A callout shows from the
// start or, with reveal 'last', as the step after the last card.
export function NumberedCardsLayout({ slots, step }: LayoutProps<'numbered-cards'>) {
  const { kicker, headline, cards, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  return (
    <div data-testid="layout-numbered-cards" className="sw-layout">
      <SlideHeading
        layout="numbered-cards"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="numbered-cards-body" className={`sw-body ${styles.body}`}>
        <ol className={styles.cards} {...cardListAttrs(cards)}>
          {cards.map((card, i) => {
            const toned = card.tone !== undefined;
            return (
              <Reveal key={i} as="li" at={oneByOne ? i + 1 : 0} step={step} className={styles.item}>
                <div
                  data-testid="numbered-cards-card"
                  className={styles.card}
                  data-tone={card.tone !== undefined ? panelTone(card.tone) : undefined}
                  data-fill={toned ? (card.fill ?? 'soft') : undefined}
                  data-pinned={card.pin !== undefined ? 'true' : undefined}
                  style={card.tone === undefined ? undefined : cardStyle(card.tone, card.fill)}
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
                  {card.pin !== undefined ? <Pin text={card.pin} /> : null}
                </div>
              </Reveal>
            );
          })}
        </ol>
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

// A toned card: the tone's pair, or with fill 'plain' the card's own surface
// under a rule in the tone's colour.
function cardStyle(tone: Tone, fill?: PanelFill): CSSProperties {
  if (fill === 'plain') return { borderTopColor: `var(--sw-${toneRule(tone)})` };
  return toneStyle(tone, fill);
}
