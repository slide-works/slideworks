import type { BulletItem } from '../slots';
import styles from './LeadText.module.css';

type LeadTextProps = {
  item: BulletItem;
  // Overrides the lead's look where a layout sets it differently (practice's 600 weight).
  leadClassName?: string;
};

// A point's text: plain text, or a bold lead and the text after it, as plain
// text nodes. It renders no wrapper, so the placing element sets the font.
export function LeadText({ item, leadClassName }: LeadTextProps) {
  if (typeof item === 'string') return item;
  return (
    <>
      <b className={leadClassName ?? styles.lead}>{item.lead}</b> {item.text}
    </>
  );
}
