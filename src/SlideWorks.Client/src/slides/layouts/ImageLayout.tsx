import { useState, type CSSProperties } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import styles from './ImageLayout.module.css';

type Natural = { width: number; height: number };

// A headline over one large uploaded image (K11) with its caption, and up to
// four points beside it. The image is never cropped: it shrinks to the space
// left, keeping its aspect ratio, and grows to at most twice its own size.
export function ImageLayout({ slots }: LayoutProps<'image'>) {
  const { kicker, headline, image, points, caption } = slots;
  const [natural, setNatural] = useState<Natural | null>(null);
  const size =
    natural === null
      ? undefined
      : ({
          '--sw-image-max': `${2 * natural.width}px`,
          '--sw-image-ratio': natural.width / natural.height,
        } as CSSProperties);
  const hasPoints = points !== undefined && points.length > 0;
  return (
    <div data-testid="layout-image" className="sw-layout">
      <SlideHeading layout="image" kicker={kicker} headline={headline} />
      <div
        data-testid="image-body"
        data-points={hasPoints ? 'true' : undefined}
        className={`sw-body ${styles.columns}`}
      >
        <figure className={styles.figure}>
          <div className={styles.frame}>
            <img
              data-testid="image-picture"
              className={styles.image}
              style={size}
              src={`/api/images/${encodeURIComponent(image.image)}`}
              alt={image.alt}
              onLoad={(event) => {
                const { naturalWidth, naturalHeight } = event.currentTarget;
                if (naturalWidth > 0 && naturalHeight > 0) {
                  setNatural({ width: naturalWidth, height: naturalHeight });
                }
              }}
            />
          </div>
          {caption !== undefined ? (
            <figcaption data-testid="image-caption" className={styles.caption}>
              {caption}
            </figcaption>
          ) : null}
        </figure>
        {hasPoints ? (
          <ul className={styles.points}>
            {points.map((point, i) => (
              <li key={i} data-testid="image-point" className={styles.point}>
                {point}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </div>
  );
}
