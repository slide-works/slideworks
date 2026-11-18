import styles from './Stamp.module.css';

type StampProps = {
  text: string;
  // 'corner' (the default) is placed against the slide frame and takes no flow
  // space; 'inline' sits in the flow, in the colour a panel sets as --panel-stamp.
  placement?: 'corner' | 'inline';
};

// A tilted stamp, such as 'Draft'.
export function Stamp({ text, placement = 'corner' }: StampProps) {
  return (
    <span
      data-testid="block-stamp"
      data-placement={placement === 'inline' ? 'inline' : undefined}
      className={placement === 'inline' ? `${styles.stamp} ${styles.inline}` : styles.stamp}
    >
      {text}
    </span>
  );
}
