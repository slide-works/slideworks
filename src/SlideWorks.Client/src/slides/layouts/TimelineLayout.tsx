import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import styles from './TimelineLayout.module.css';

// Events down the slide, each a short when and what happens then.
export function TimelineLayout({ slots }: LayoutProps<'timeline'>) {
  const { kicker, headline, events } = slots;
  return (
    <div data-testid="layout-timeline" className="sw-layout">
      <SlideHeading
        layout="timeline"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <ol data-testid="timeline-body" className={`sw-body ${styles.events}`}>
        {events.map((event, index) => (
          <li key={index} data-testid="timeline-event" className={styles.event}>
            <span className={styles.when}>{event.when}</span>
            <span className={styles.text}>{event.text}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}
