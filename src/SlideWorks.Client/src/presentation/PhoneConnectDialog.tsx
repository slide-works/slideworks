import type { PresentationConnection } from './types';

type Props = {
  remoteUrl: string;
  open: boolean;
  onClose(): void;
  // The presenter window passes the connected phones: then the dialog shows a chip per phone and closes itself when
  // the first one connects. The deck page passes none.
  phones?: PresentationConnection[];
};

// "Use your phone as a remote": the QR code and the link a phone opens (P3's remoteUrl).
export function PhoneConnectDialog({ remoteUrl, open, onClose }: Props) {
  if (!open) return null;
  // TODO: placeholder dialog until the phone dialog is built: the QR code, the copy button, the phone chips and
  // closing on the first phone replace it.
  return (
    <div
      data-testid="phone-connect"
      role="dialog"
      aria-label="Use your phone as a remote"
      className="panel grid gap-3 p-4"
    >
      <p data-testid="phone-connect-url" className="text-meta text-ink">
        {remoteUrl}
      </p>
      <button type="button" className="btn-ghost" onClick={onClose}>
        Close
      </button>
    </div>
  );
}
