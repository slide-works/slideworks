import { useState, type CSSProperties } from 'react';
import { LeadText } from '../blocks/LeadText';
import type { ColumnSlot } from '../slots';
import styles from './TwoColumnColumn.module.css';

type TwoColumnColumnProps = { side: 'left' | 'right'; column: ColumnSlot };

// One column of the two-column layout: an optional title over a list of
// points, a paragraph or an uploaded image (K11).
export function TwoColumnColumn({ side, column }: TwoColumnColumnProps) {
  const { title, items, text, image } = column;
  return (
    <div data-testid={`two-column-${side}`} className={styles.column}>
      {title !== undefined ? (
        <h3 data-testid="two-column-title" className={styles.title}>
          {title}
        </h3>
      ) : null}
      {items !== undefined ? (
        <ul className={styles.items}>
          {items.map((item, i) => (
            <li key={i} data-testid="two-column-item" className={styles.item}>
              <LeadText item={item} />
            </li>
          ))}
        </ul>
      ) : null}
      {text !== undefined ? (
        <p data-testid="two-column-text" className={styles.text}>
          {text}
        </p>
      ) : null}
      {image !== undefined ? <ColumnImage id={image.image} alt={image.alt} /> : null}
    </div>
  );
}

type Natural = { width: number; height: number };

// The image fills the column's width and shrinks to the height that is left,
// keeping its aspect ratio, but never grows past twice its own size; a small
// image sits centred. Its box is the picture, so the white backing, shadow
// and rounded corners sit on the picture itself.
function ColumnImage({ id, alt }: { id: string; alt: string }) {
  const [natural, setNatural] = useState<Natural | null>(null);
  const size =
    natural === null
      ? undefined
      : ({
          '--sw-image-max': `${2 * natural.width}px`,
          '--sw-image-ratio': natural.width / natural.height,
        } as CSSProperties);
  return (
    <figure className={styles.figure}>
      <img
        data-testid="two-column-image"
        className={styles.image}
        style={size}
        src={`/api/images/${encodeURIComponent(id)}`}
        alt={alt}
        onLoad={(event) => {
          const { naturalWidth, naturalHeight } = event.currentTarget;
          if (naturalWidth > 0 && naturalHeight > 0) {
            setNatural({ width: naturalWidth, height: naturalHeight });
          }
        }}
      />
    </figure>
  );
}
