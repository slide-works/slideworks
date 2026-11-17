import { QrCode } from '../../qr/QrCode';
import { useJoin } from '../JoinContext';

// The join bar (SW-5 D5): a small QR code and the code, bottom right above the progress bar, on every slide while the
// deck is live and its join bar is on. The frame decides when to draw it.
export function JoinBar() {
  const joined = useJoin();
  if (joined === null || !joined.live) return null;
  return (
    <div className="sw-join-bar" data-testid="join-bar">
      <div className="sw-join-bar-qr" data-testid="join-bar-qr">
        <QrCode text={joined.join.joinUrl} label="Join on your phone" />
      </div>
      <div className="sw-join-bar-text">
        <small>{joined.join.hostLabel}</small>
        <b data-testid="join-bar-code">{joined.join.codeLabel}</b>
      </div>
    </div>
  );
}
