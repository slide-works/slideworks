import { useEffect, useRef, useState } from 'react';
import type { FocusEvent, KeyboardEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { downloadFile } from '../../api/downloadFile';
import type { DeckPageView } from '../../api/types';
import { PhoneConnectDialog } from '../../presentation/PhoneConnectDialog';

// The split Present button. ▶ Present opens the player as it always did. For anyone who can edit (owner or write) the arrow opens
// four ways to present (PNG 03): the player, the presenter view, and the phone remote, and the PDF download. Opening the menu moves focus
// to its first item; Escape closes it and returns focus to the arrow; a click outside or tabbing out closes it.
export function PresentMenu({ view }: { view: DeckPageView }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [phoneOpen, setPhoneOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const arrowRef = useRef<HTMLButtonElement>(null);
  const firstItemRef = useRef<HTMLAnchorElement>(null);
  const pdfRef = useRef<HTMLButtonElement>(null);
  const [pdfRunning, setPdfRunning] = useState(false);
  const [pdfError, setPdfError] = useState<string | null>(null);

  // The menu closes as the dialog opens, so the dialog's own focus restore finds nothing to return to: when the
  // dialog closes, focus goes back to the arrow.
  const phoneWasOpen = useRef(false);
  useEffect(() => {
    if (phoneWasOpen.current && !phoneOpen) arrowRef.current?.focus();
    phoneWasOpen.current = phoneOpen;
  }, [phoneOpen]);

  useEffect(() => {
    if (!open) return;
    firstItemRef.current?.focus();
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener('pointerdown', closeOnOutsidePointer);
    return () => document.removeEventListener('pointerdown', closeOnOutsidePointer);
  }, [open]);

  function closeOnEscape(event: KeyboardEvent) {
    if (open && event.key === 'Escape') {
      event.preventDefault();
      setOpen(false);
      arrowRef.current?.focus();
    }
  }

  function closeOnFocusLeaving(event: FocusEvent) {
    const next = event.relatedTarget;
    if (next instanceof Node && !rootRef.current?.contains(next)) setOpen(false);
  }

  // The audience window opens in the same click handler as the navigation: that is the only reliable way past a
  // popup blocker (D10).
  function startPresenterView() {
    setOpen(false);
    window.open(`/d/${view.id}/${view.slug}?present`, `sw-audience-${view.id}`);
    navigate(`/decks/${view.id}/present`);
  }

  // The menu stays open while the PDF is made. aria-disabled (not disabled) keeps focus on the item; clicks are ignored
  // meanwhile. Success closes the menu and returns focus to the arrow; failure shows the error under the item.
  async function downloadPdf() {
    if (pdfRunning) return;
    setPdfRunning(true);
    setPdfError(null);
    try {
      await downloadFile(`/api/decks/${view.id}/pdf`, `${view.slug}.pdf`);
      setOpen(false);
      arrowRef.current?.focus();
    } catch (error) {
      setPdfError(error instanceof Error ? error.message : String(error));
      pdfRef.current?.focus();
    } finally {
      setPdfRunning(false);
    }
  }

  const present = (
    <Link to={view.playUrl} data-testid="deck-present" className="btn-primary no-underline">
      ▶ Present
    </Link>
  );
  if (!view.canEdit) return present;

  return (
    <div ref={rootRef} className="relative" onKeyDown={closeOnEscape} onBlur={closeOnFocusLeaving}>
      <div className="flex items-stretch gap-px">
        <Link
          to={view.playUrl}
          data-testid="deck-present"
          className="btn-primary rounded-r-none no-underline"
        >
          ▶ Present
        </Link>
        <button
          ref={arrowRef}
          type="button"
          data-testid="present-menu"
          aria-label="More ways to present"
          aria-haspopup="menu"
          aria-expanded={open}
          className="btn-primary rounded-l-none px-2"
          onClick={() => setOpen((value) => !value)}
        >
          ▾
        </button>
      </div>
      {open && (
        <div
          role="menu"
          aria-label="More ways to present"
          className="absolute right-0 z-10 mt-2 grid w-72 gap-0.5 rounded-panel border border-edge bg-panel p-1.5 shadow-lg"
        >
          <Link
            ref={firstItemRef}
            role="menuitem"
            to={view.playUrl}
            data-testid="present-menu-player"
            className="grid rounded-panel px-2.5 py-2 text-ink no-underline hover:bg-panel-2"
          >
            <b className="text-meta">Present</b>
            <span className="text-xs text-ink-muted">The player in this tab.</span>
          </Link>
          <button
            type="button"
            role="menuitem"
            data-testid="present-menu-presenter-view"
            className="grid rounded-panel px-2.5 py-2 text-left text-ink hover:bg-panel-2"
            onClick={startPresenterView}
          >
            <b className="text-meta">Presenter view</b>
            <span className="text-xs text-ink-muted">
              Notes, next slide and timer here. Opens the audience window to drag onto the
              projector.
            </span>
          </button>
          <button
            type="button"
            role="menuitem"
            data-testid="present-menu-phone"
            className="grid rounded-panel px-2.5 py-2 text-left text-ink hover:bg-panel-2"
            onClick={() => {
              setOpen(false);
              setPhoneOpen(true);
            }}
          >
            <b className="text-meta">Control from your phone</b>
            <span className="text-xs text-ink-muted">Shows a QR code that opens the remote.</span>
          </button>
          <button
            ref={pdfRef}
            type="button"
            role="menuitem"
            data-testid="present-menu-pdf"
            aria-label={`Download PDF of ${view.title}`}
            aria-disabled={pdfRunning}
            className="grid rounded-panel px-2.5 py-2 text-left text-ink hover:bg-panel-2"
            onClick={downloadPdf}
          >
            <b className="text-meta">Download PDF</b>
            <span className="text-xs text-ink-muted">
              {pdfRunning
                ? 'Making the PDF…'
                : "One page per slide, every reveal shown, with the last talk's results."}
            </span>
          </button>
          {pdfError !== null && (
            <p
              role="alert"
              data-testid="present-menu-pdf-error"
              className="px-2.5 pb-1 text-xs text-bad"
            >
              {pdfError}
            </p>
          )}
        </div>
      )}
      <PhoneConnectDialog
        remoteUrl={`${window.location.origin}/remote?deck=${view.id}`}
        open={phoneOpen}
        onClose={() => setPhoneOpen(false)}
      />
    </div>
  );
}
