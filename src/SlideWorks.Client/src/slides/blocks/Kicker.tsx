import styles from './Kicker.module.css';

type KickerProps = {
  // Its test id, such as `bullets-kicker` or `quote-kicker`.
  testId: string;
  // Nothing is drawn when there is no kicker.
  text: string | undefined;
};

// The kicker a content slide opens with, over its headline (SlideHeading) or
// over a quote. Light slides only: the title slide's kicker is its own.
export function Kicker({ testId, text }: KickerProps) {
  if (text === undefined) return null;
  return (
    <p data-testid={testId} className={styles.kicker}>
      {text}
    </p>
  );
}
