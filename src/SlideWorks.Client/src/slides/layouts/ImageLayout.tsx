import { useState, type CSSProperties } from 'react';
import { BodyCallout } from '../blocks/BodyCallout';
import { Icon } from '../blocks/Icon';
import { iconFor } from '../blocks/icons';
import { LeadText } from '../blocks/LeadText';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { safeHref } from '../safeHref';
import { calloutStep } from '../steps';
import { videoEmbed } from '../videoEmbed';
import styles from './ImageLayout.module.css';

type Natural = { width: number; height: number };

// The side of an icon in a point's mark place.
const ICON_SIZE = 34;

// A headline over one large uploaded image (K11) with its caption, and up to
// four points beside it. The image is never cropped: it shrinks to the space
// left, keeping its aspect ratio, and grows to at most twice its own size.
// `frame: 'none'` draws it without its card (for a transparent image) and
// `position: 'right'` puts it beside the headline at full height, with the
// headline, points and callout in a left column. With reveal 'one-by-one'
// point n appears at step n; a callout shows from the start or, with reveal
// 'last', as the step after the last point.
export function ImageLayout({ slots, step }: LayoutProps<'image'>) {
  const { kicker, headline, image, frame, position, points, caption, link, reveal, callout } = slots;
  const [natural, setNatural] = useState<Natural | null>(null);
  const size =
    natural === null
      ? undefined
      : ({
          '--sw-image-max': `${2 * natural.width}px`,
          '--sw-image-ratio': natural.width / natural.height,
        } as CSSProperties);
  const hasPoints = points !== undefined && points.length > 0;
  const oneByOne = reveal === 'one-by-one';
  const heading = (
    <SlideHeading
      layout="image"
      kicker={kicker}
      headline={headline}
      emphasis={emphasisFor(slots, 'headline')}
    />
  );
  const figure = (
    <figure className={styles.figure}>
      <div className={styles.frame}>
        <Picture image={image} link={link} size={size} onLoad={setNatural} />
      </div>
      {caption !== undefined ? (
        <figcaption data-testid="image-caption" className={styles.caption}>
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
  const list = hasPoints ? (
    <ul className={styles.points}>
      {points.map((point, i) => {
        // An unknown icon name draws nothing and keeps the normal mark (decide on iconFor, not on the slot).
        const name =
          typeof point === 'string' || point.icon === undefined || iconFor(point.icon) === undefined
            ? undefined
            : point.icon;
        const className = name === undefined ? styles.point : `${styles.point} ${styles.iconed}`;
        const mark =
          name !== undefined ? (
            <span data-testid="image-point-icon" className={styles.icon}>
              <Icon name={name} size={ICON_SIZE} />
            </span>
          ) : null;
        return oneByOne ? (
          <Reveal key={i} as="li" at={i + 1} step={step} className={className}>
            {mark}
            <span data-testid="image-point">
              <LeadText item={point} />
            </span>
          </Reveal>
        ) : (
          <li key={i} data-testid="image-point" className={className}>
            {mark}
            <LeadText item={point} />
          </li>
        );
      })}
    </ul>
  ) : null;
  const note =
    callout !== undefined ? (
      <BodyCallout
        callout={callout}
        step={step}
        at={calloutStep(oneByOne && hasPoints ? points.length : 0, callout)}
      />
    ) : null;
  if (position === 'right') {
    return (
      <div
        data-testid="layout-image"
        data-frame={frame ?? 'card'}
        data-position="right"
        className="sw-layout"
      >
        <div data-testid="image-body" className={styles.beside}>
          <div className={styles.left}>
            {heading}
            {list}
            {note !== null ? <div className={styles.foot}>{note}</div> : null}
          </div>
          {figure}
        </div>
      </div>
    );
  }
  return (
    <div
      data-testid="layout-image"
      data-frame={frame ?? 'card'}
      data-position="below"
      className="sw-layout"
    >
      {heading}
      <div
        data-testid="image-body"
        data-points={hasPoints ? 'true' : undefined}
        data-callout={note !== null ? 'true' : undefined}
        className={`sw-body ${styles.columns}`}
      >
        {figure}
        {list}
        {note !== null ? <div className={styles.under}>{note}</div> : null}
      </div>
    </div>
  );
}

// The uploaded image. A YouTube or Vimeo link plays on the slide, with the image behind it. Any other link opens
// from the image. The anchor uses display: contents, so a link does not change the image's box.
function Picture({
  image,
  link,
  size,
  onLoad,
}: {
  image: LayoutProps<'image'>['slots']['image'];
  link: string | undefined;
  size: CSSProperties | undefined;
  onLoad: (natural: Natural) => void;
}) {
  const img = (
    <img
      data-testid="image-picture"
      className={styles.image}
      style={size}
      src={`/api/images/${encodeURIComponent(image.image)}`}
      alt={image.alt}
      onLoad={(event) => {
        const { naturalWidth, naturalHeight } = event.currentTarget;
        if (naturalWidth > 0 && naturalHeight > 0) {
          onLoad({ width: naturalWidth, height: naturalHeight });
        }
      }}
    />
  );
  const embed = videoEmbed(link);
  if (embed !== undefined) {
    return (
      <>
        {img}
        <iframe
          data-testid="image-video"
          className={styles.video}
          src={embed}
          title={image.alt}
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          allowFullScreen
        />
      </>
    );
  }
  const href = safeHref(link);
  if (href === undefined) return img;
  return (
    <a data-testid="image-link" className={styles.link} href={href} target="_blank" rel="noopener noreferrer">
      {img}
    </a>
  );
}
