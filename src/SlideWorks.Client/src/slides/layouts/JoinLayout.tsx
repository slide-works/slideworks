import { QrCode } from '../../qr/QrCode';
import { useJoin } from '../JoinContext';
import type { LayoutProps } from '../slots';
import { DEFAULT_JOIN_KICKER } from './joinKicker';
import styles from './JoinLayout.module.css';

// The join slide (dark): the deck's join code and address, read from JoinContext, never from the slots (SW-5 J6).
// The QR code is 780 px on the canvas, pure black on white whatever the look. Before Go live the slide shows the
// deck's standing code and says it isn't live yet; once live it shows how many have joined, unless the count slot
// hides it.
export function JoinLayout({ slots }: LayoutProps<'join'>) {
  const joined = useJoin();
  return (
    <div data-testid="layout-join" className="sw-layout">
      <div className={styles.join}>
        <div data-testid="join-qr" className={styles.qr}>
          {joined === null ? null : (
            <QrCode text={joined.join.joinUrl} label="Join on your phone" />
          )}
        </div>
        <div className={styles.text}>
          <p data-testid="join-kicker" className={styles.kicker}>
            {slots.kicker ?? DEFAULT_JOIN_KICKER}
          </p>
          <p data-testid="join-code" className={styles.code}>
            {joined?.join.codeLabel}
          </p>
          <p data-testid="join-host" className={styles.host}>
            {joined?.join.hostLabel}
          </p>
          {joined?.live ? (
            slots.count === 'hidden' ? null : (
              <p data-testid="join-count" className={styles.count}>
                {joined.joinedCount} joined
              </p>
            )
          ) : (
            <p data-testid="join-not-live" className={styles.notLive}>
              Not live yet: early joiners wait in the lobby
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
