import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import styles from './ColumnsListLayout.module.css';

// Several columns of bullets under one headline.
export function ColumnsListLayout({ slots }: LayoutProps<'columns-list'>) {
  const { kicker, headline, columns } = slots;
  return (
    <div data-testid="layout-columns-list" className="sw-layout">
      <SlideHeading
        layout="columns-list"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div
        data-testid="columns-list-body"
        className={`sw-body ${styles.columns}`}
        style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
      >
        {columns.map((column, index) => (
          <section key={index} data-testid="columns-list-column" className={styles.column}>
            <h3 className={styles.title}>{column.title}</h3>
            <ul className={styles.items}>
              {column.items.map((item, itemIndex) => (
                <li key={itemIndex}>{item}</li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
