import { useEffect, useId, useRef } from 'react';
import { ModalDialog } from '../components/sharing/ModalDialog';
import { useCopyText } from '../components/useCopyText';
import type { PresentationConnection } from './types';
import { QrCode } from './QrCode';

type Props = {
  remoteUrl: string;
  open: boolean;
  onClose(): void;
  // The presenter window passes the connected phones: then the dialog shows a chip per phone and closes itself when
  // the first one connects. The deck page passes none.
  phones?: PresentationConnection[];
};

// "Use your phone as a remote": the QR code and the link a phone opens (P3's remoteUrl).
export function PhoneConnectDialog({ open, ...rest }: Props) {
  if (!open) return null;
  return <OpenPhoneConnectDialog {...rest} />;
}

function OpenPhoneConnectDialog({ remoteUrl, onClose, phones }: Omit<Props, 'open'>) {
  const titleId = useId();
  const descriptionId = useId();
  const urlRef = useRef<HTMLElement>(null);
  const { state, copy } = useCopyText(urlRef);
  const count = phones?.length ?? 0;
  const countAtOpen = useRef(count);

  // A phone that connects while the dialog is open closes it; phones already connected when it opened do not.
  useEffect(() => {
    if (phones && countAtOpen.current === 0 && count > 0) onClose();
    countAtOpen.current = count;
  }, [phones, count, onClose]);

  return (
    <ModalDialog
      labelledBy={titleId}
      describedBy={descriptionId}
      testId="phone-connect"
      widthClassName="max-w-[560px]"
      onClose={onClose}
    >
      <div className="flex flex-wrap gap-5 px-[18px] pt-4 pb-3">
        <QrCode text={remoteUrl} label="QR code that opens the phone remote" />
        <div className="min-w-0 flex-1 basis-56">
          <h2 id={titleId} className="text-base font-semibold text-ink">
            Control this talk from your phone
          </h2>
          <ol
            id={descriptionId}
            className="mt-2 grid list-decimal gap-1.5 pl-5 text-meta text-ink-muted"
          >
            <li>Scan the code, or open the link below.</li>
            <li>Sign in with the same account (once per phone; it stays signed in).</li>
            <li>Tap this deck. The phone shows your notes and drives this laptop.</li>
          </ol>
          <div className="mt-3 flex items-center gap-2">
            <code
              ref={urlRef}
              data-testid="phone-connect-url"
              className="min-w-0 flex-1 truncate rounded-panel bg-panel-2 px-2 py-1 text-meta text-ink select-all"
            >
              {remoteUrl}
            </code>
            <button
              type="button"
              data-testid="phone-connect-copy"
              className="btn-ghost"
              onClick={() => copy(remoteUrl)}
            >
              {state === 'copied' ? 'Copied' : state === 'failed' ? 'Select and copy' : 'Copy'}
            </button>
          </div>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-2 px-[18px] pb-4">
        {phones?.map((phone) => (
          <span
            key={phone.clientId}
            data-testid="phone-connect-chip"
            className="inline-flex items-center gap-1.5 rounded-full bg-ok-soft px-2.5 py-1 text-xs font-semibold text-ok"
          >
            {phone.label} connected
          </span>
        ))}
        <button
          type="button"
          data-testid="phone-connect-close"
          className="btn-ghost ml-auto"
          onClick={onClose}
        >
          Close
        </button>
      </div>
    </ModalDialog>
  );
}
