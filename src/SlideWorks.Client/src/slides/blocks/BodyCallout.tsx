import { Reveal } from '../Reveal';
import type { CalloutBlock } from '../slots';
import styles from './BodyCallout.module.css';
import { Callout } from './Callout';

type BodyCalloutProps = {
  callout: CalloutBlock;
  // The slide's current step.
  step: number;
  // The step the callout appears at, from calloutStep; 0 shows it from the start.
  at: number;
};

// The callout a layout draws under its content (K9). When it has a step of its
// own it sits in a Reveal, so it keeps its space while hidden and nothing
// above it moves when it appears.
export function BodyCallout({ callout, step, at }: BodyCalloutProps) {
  return (
    <div data-testid="body-callout" className={styles.body}>
      {at === 0 ? (
        <Callout {...callout} />
      ) : (
        <Reveal at={at} step={step}>
          <Callout {...callout} />
        </Reveal>
      )}
    </div>
  );
}
