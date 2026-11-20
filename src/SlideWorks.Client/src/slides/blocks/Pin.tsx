import styles from './Pin.module.css';

// A short label pinned to a highlighted card or panel, such as "Hold on to
// this one". The placing card positions it (bottom right, 28 px in).
export function Pin({ text }: { text: string }) {
  return (
    <span data-testid="pin" className={styles.pin}>
      {text}
    </span>
  );
}
