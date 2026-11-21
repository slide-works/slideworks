import { BodyCallout } from '../blocks/BodyCallout';
import { Icon } from '../blocks/Icon';
import { iconFor } from '../blocks/icons';
import { LeadText } from '../blocks/LeadText';
import { SlideHeading } from '../blocks/SlideHeading';
import { calloutStep } from '../steps';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './BulletsLayout.module.css';

const ICON_SIZE = 40;

// A headline over 1–8 points, each plain text or an optional icon, bold lead
// and text, with an optional callout under them. With reveal 'one-by-one'
// point n appears at step n; the callout shows from the start, or at the step
// after the last point when its reveal is 'last'.
export function BulletsLayout({ slots, step }: LayoutProps<'bullets'>) {
  const { kicker, headline, items, style, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  const iconName = (item: (typeof items)[number]) =>
    typeof item === 'string' || item.icon === undefined || iconFor(item.icon) === undefined
      ? undefined
      : item.icon;
  const withIcons = items.some((item) => iconName(item) !== undefined);
  const base = withIcons ? `${styles.item} ${styles.withIcon}` : styles.item;
  return (
    <div data-testid="layout-bullets" className="sw-layout">
      <SlideHeading
        layout="bullets"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="bullets-body" className="sw-body">
        <ul className={styles.items} data-style={style === 'numbered' ? 'numbered' : undefined}>
          {items.map((item, i) => {
            const name = iconName(item);
            const className = name === undefined ? base : `${base} ${styles.iconed}`;
            const text = (
              <>
                {name !== undefined ? (
                  <span data-testid="bullets-icon" className={styles.icon}>
                    <Icon name={name} size={ICON_SIZE} />
                  </span>
                ) : null}
                <span data-testid="bullets-item">
                  <LeadText item={item} />
                </span>
              </>
            );
            return oneByOne ? (
              <Reveal key={i} as="li" at={i + 1} step={step} className={className}>
                {text}
              </Reveal>
            ) : (
              <li key={i} className={className}>
                {text}
              </li>
            );
          })}
        </ul>
        {callout !== undefined ? (
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? items.length : 0, callout)}
          />
        ) : null}
      </div>
    </div>
  );
}
