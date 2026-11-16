import type { BulletItem, ColumnSlot, LayoutProps } from '../slots';
import styles from './TwoColumnLayout.module.css';

// A headline over two columns. Each column has an optional title and exactly
// one of a list of points, a paragraph or an uploaded image.
export function TwoColumnLayout({ slots }: LayoutProps<'two-column'>) {
  const { kicker, headline, left, right } = slots;
  return (
    <div data-testid="layout-two-column" className="sw-layout">
      {kicker !== undefined ? (
        <p data-testid="two-column-kicker" className={styles.kicker}>
          {kicker}
        </p>
      ) : null}
      <h2 data-testid="two-column-headline" className={styles.headline}>
        {headline}
      </h2>
      <div data-testid="two-column-body" className={`sw-body ${styles.columns}`}>
        <Column side="left" column={left} />
        <Column side="right" column={right} />
      </div>
    </div>
  );
}

function Column({ side, column }: { side: 'left' | 'right'; column: ColumnSlot }) {
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
              <ItemText item={item} />
            </li>
          ))}
        </ul>
      ) : null}
      {text !== undefined ? (
        <p data-testid="two-column-text" className={styles.text}>
          {text}
        </p>
      ) : null}
      {image !== undefined ? (
        <figure className={styles.figure}>
          <img
            data-testid="two-column-image"
            className={styles.image}
            src={`/api/images/${encodeURIComponent(image.image)}`}
            alt={image.alt}
          />
        </figure>
      ) : null}
    </div>
  );
}

function ItemText({ item }: { item: BulletItem }) {
  return typeof item === 'string' ? (
    item
  ) : (
    <>
      <b className={styles.lead}>{item.lead}</b> {item.text}
    </>
  );
}
