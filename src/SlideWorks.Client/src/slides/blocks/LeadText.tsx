import type { BulletItem } from '../slots';
import styles from './LeadText.module.css';

type LeadTextProps = {
  item: BulletItem;
  // Overrides the lead's look where a layout sets it differently (practice's 600 weight).
  leadClassName?: string;
};

// A point's text: plain text, or a bold lead and the text after it, as plain
// text nodes. It renders no wrapper, so the placing element sets the font. An
// item's icon is the placing layout's to draw, in its mark's place.
export function LeadText({ item, leadClassName }: LeadTextProps) {
  if (typeof item === 'string') return item;
  if (item.lead === undefined) return item.text;
  return (
    <>
      <b className={leadClassName ?? styles.lead}>{item.lead}</b> {item.text}
    </>
  );
}
