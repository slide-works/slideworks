import { emphasisPieces } from '../emphasis';
import type { Emphasis } from '../slots/blocks';
import styles from './Emphasised.module.css';

type EmphasisedProps = {
  text: string;
  // The phrases of this text to draw in a tone (emphasisFor picks them).
  spans?: readonly Emphasis[];
};

// A text with some of its phrases in a tone (K10). The text is split where
// each phrase stands and drawn as text nodes and spans, so it is never read as
// markup and its text content is the text itself. Without spans it is the bare
// text node.
export function Emphasised({ text, spans }: EmphasisedProps) {
  if (spans === undefined || spans.length === 0) return text;
  return emphasisPieces(text, spans).map((piece, i) =>
    piece.tone === undefined ? (
      piece.text
    ) : (
      <span key={i} data-testid="emphasis" data-tone={piece.tone} className={styles.phrase}>
        {piece.text}
      </span>
    ),
  );
}
