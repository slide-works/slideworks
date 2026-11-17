import { useJoin } from '../JoinContext';
import type { LayoutProps } from '../slots';
import styles from './JoinLayout.module.css';

const DEFAULT_KICKER = 'Join on your phone';

// The join slide (dark): the deck's join code and address, read from JoinContext, never from the slots (SW-5 J6).
// TODO: replace this stub with the full layout: the QR code at 780 px, the address and the live count.
export function JoinLayout({ slots }: LayoutProps<'join'>) {
  const joined = useJoin();
  return (
    <div data-testid="layout-join" className={`sw-layout ${styles.join}`}>
      <p data-testid="join-kicker" className={styles.kicker}>
        {slots.kicker ?? DEFAULT_KICKER}
      </p>
      <p data-testid="join-code" className={styles.code}>
        {joined?.join.codeLabel}
      </p>
      <p data-testid="join-host" className={styles.host}>
        {joined?.join.hostLabel}
      </p>
      {joined?.live && slots.count !== 'hidden' ? (
        <p data-testid="join-count" className={styles.count}>
          {joined.joinedCount} joined
        </p>
      ) : null}
    </div>
  );
}
