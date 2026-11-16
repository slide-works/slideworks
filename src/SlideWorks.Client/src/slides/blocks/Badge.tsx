import styles from './Badge.module.css';

// A short label at the top of a card, such as a role. Badges carry no tone
// (SW-19 D5), so every badge takes the accent.
export function Badge({ text }: { text: string }) {
  return (
    <span data-testid="block-badge" className={styles.badge}>
      {text}
    </span>
  );
}
