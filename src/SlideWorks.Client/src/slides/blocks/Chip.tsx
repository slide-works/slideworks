import styles from './Chip.module.css';

// A small rounded tag, such as a tool or a practice.
export function Chip({ text }: { text: string }) {
  return (
    <span data-testid="block-chip" className={styles.chip}>
      {text}
    </span>
  );
}
