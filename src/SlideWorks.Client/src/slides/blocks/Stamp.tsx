import styles from './Stamp.module.css';

// A tilted stamp in the slide's top right corner, such as 'Draft'. It is
// absolutely placed against the slide frame, so it takes no flow space.
export function Stamp({ text }: { text: string }) {
  return (
    <span data-testid="block-stamp" className={styles.stamp}>
      {text}
    </span>
  );
}
