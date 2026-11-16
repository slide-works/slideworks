import type { CalloutBlock } from '../slots';
import styles from './Callout.module.css';
import { panelTone, toneStyle } from './tone';

// A short statement in a toned box (SW-2 L4). Its tone and fill pick the
// palette's surface and ink pair; data-tone keeps the tone as written, so the
// aliases good, bad and info stay as stored.
export function Callout({ tone, fill = 'soft', text }: CalloutBlock) {
  return (
    <p
      data-testid="block-callout"
      data-tone={tone}
      data-panel={panelTone(tone)}
      data-fill={fill}
      className={styles.callout}
      style={toneStyle(tone, fill)}
    >
      {text}
    </p>
  );
}
