import { SlideHeading } from '../blocks/SlideHeading';
import { QrCode } from '../../qr/QrCode';
import { emphasisFor } from '../emphasis';
import { safeHref } from '../safeHref';
import type { LayoutProps } from '../slots';
import type { LinkItem } from '../slots/links';
import styles from './LinksLayout.module.css';

const SCHEME = /^https?:\/\//i;

// One title: an anchor that opens the address in a new tab when safeHref
// accepts it, plain text when it does not. The address only ever becomes an
// attribute here, never HTML, and a refused one is drawn as the text it is.
function LinkTitle({ link, interactive }: { link: LinkItem; interactive: boolean }) {
  const href = safeHref(link.url);
  if (href === undefined) {
    return (
      <div data-testid="links-title" className={styles.title}>
        {link.title}
      </div>
    );
  }
  return (
    <div className={styles.title}>
      <a
        data-testid="links-title"
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        tabIndex={interactive ? undefined : -1}
        className={styles.anchor}
      >
        {link.title}
      </a>
    </div>
  );
}

// A list of 1–6 links, each a kind, a title and a source, with an optional QR
// code beside it for one address. No steps, no callout, no tracker.
export function LinksLayout({ slots, deck }: LayoutProps<'links'>) {
  const { kicker, headline, links, qr } = slots;
  const interactive = deck.interactive !== false;
  const qrHref = qr === undefined ? undefined : safeHref(qr.url);
  const caption = qr === undefined ? '' : (qr.caption ?? qr.url.replace(SCHEME, ''));
  return (
    <div
      data-testid="layout-links"
      data-interactive={interactive ? 'true' : 'false'}
      className={`sw-layout ${styles.links}`}
    >
      <SlideHeading
        layout="links"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div className={`sw-body ${styles.body}`}>
        <ul
          data-testid="links-list"
          data-qr={qrHref === undefined ? 'false' : 'true'}
          className={styles.list}
        >
          {links.map((link, index) => (
            <li key={index} data-testid="links-row" className={styles.row}>
              <span data-testid="links-kind" className={styles.kind}>
                {link.kind}
              </span>
              <div className={styles.text}>
                <LinkTitle link={link} interactive={interactive} />
                {link.source !== undefined && (
                  <p data-testid="links-source" className={styles.source}>
                    {link.source}
                  </p>
                )}
              </div>
            </li>
          ))}
        </ul>
        {qrHref !== undefined && (
          <figure data-testid="links-qr" className={styles.qr}>
            <div className={styles.code}>
              <QrCode text={qrHref} label={`QR code for ${caption}`} />
            </div>
            <figcaption data-testid="links-qr-caption" className={styles.caption}>
              {caption}
            </figcaption>
          </figure>
        )}
      </div>
    </div>
  );
}
