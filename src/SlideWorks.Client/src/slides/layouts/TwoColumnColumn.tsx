import { useState, type CSSProperties } from 'react';
import { Icon } from '../blocks/Icon';
import { LeadText } from '../blocks/LeadText';
import type { ColumnSlot } from '../slots';
import styles from './TwoColumnColumn.module.css';

type TwoColumnColumnProps = { side: 'left' | 'right'; column: ColumnSlot };

// The side of an icon beside the column title and in an item's mark place.
const ICON_SIZE = 34;

// One column of the two-column layout: an optional title over a list of
// points, a paragraph or an uploaded image (K11). The title may carry an icon
// beside it, and an item's icon takes the place of its bullet mark. The levels
// layout places it as its main column too.
export function TwoColumnColumn({ side, column }: TwoColumnColumnProps) {
  const { title, icon, items, text, image } = column;
  return (
    <div data-testid={`two-column-${side}`} className={styles.column}>
      {title !== undefined ? (
        icon !== undefined ? (
          <h3 data-testid="two-column-title" className={`${styles.title} ${styles.titleWithIcon}`}>
            <span data-testid="two-column-title-icon" className={styles.titleIcon}>
              <Icon name={icon} size={ICON_SIZE} />
            </span>
            <span className={styles.titleText}>{title}</span>
          </h3>
        ) : (
          <h3 data-testid="two-column-title" className={styles.title}>
            {title}
          </h3>
        )
      ) : null}
      {items !== undefined ? (
        <ul className={styles.items}>
          {items.map((item, i) => {
            const itemIcon = typeof item === 'string' ? undefined : item.icon;
            return itemIcon !== undefined ? (
              <li
                key={i}
                data-testid="two-column-item"
                className={`${styles.item} ${styles.itemWithIcon}`}
              >
                <span data-testid="two-column-item-icon" className={styles.itemIcon}>
                  <Icon name={itemIcon} size={ICON_SIZE} />
                </span>
                <LeadText item={item} />
              </li>
            ) : (
              <li key={i} data-testid="two-column-item" className={styles.item}>
                <LeadText item={item} />
              </li>
            );
          })}
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
