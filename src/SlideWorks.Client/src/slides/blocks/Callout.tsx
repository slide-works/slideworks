import type { CalloutBlock } from '../slots';
import styles from './Callout.module.css';

// A short statement in a tinted box. The tone colours it: info in the accent,
// bad and good with a red or green strip down the left edge.
export function Callout({ tone, text }: CalloutBlock) {
  return (
    <p data-testid="block-callout" data-tone={tone} className={styles.callout}>
      {text}
    </p>
  );
}
