import { useLayoutEffect, useRef, type KeyboardEvent, type ReactNode } from 'react';

type ModalDialogProps = {
  labelledBy: string;
  describedBy: string;
  testId: string;
  // A Tailwind max-width class; the sharing dialog's 520px unless a caller needs more.
  widthClassName?: string;
  onClose(): void;
  children: ReactNode;
};

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

// A modal over the page: the browser's <dialog> shown with showModal(), so the
// page behind it is inert. Tab and Shift+Tab wrap inside it, Esc closes it, and
// when it goes away focus returns to whatever had it before (the Share button).
export function ModalDialog({
  labelledBy,
  describedBy,
  testId,
  widthClassName = 'max-w-[520px]',
  onClose,
  children,
}: ModalDialogProps) {
  const ref = useRef<HTMLDialogElement>(null);

  useLayoutEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog?.showModal();
    return () => {
      if (dialog?.open) dialog.close();
      if (previous?.isConnected) previous.focus();
    };
  }, []);

  const onKeyDown = (event: KeyboardEvent<HTMLDialogElement>) => {
    if (event.key !== 'Tab' || !ref.current) return;
    const focusable = tabStops(ref.current);
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return (
    <dialog
      ref={ref}
      aria-labelledby={labelledBy}
      aria-describedby={describedBy}
      data-testid={testId}
      className={`m-auto w-[calc(100%-2rem)] ${widthClassName} overflow-y-auto rounded-xl bg-panel p-0 text-ink shadow-[0_24px_56px_-16px_rgb(16_24_40/0.4)] backdrop:bg-slate-900/30`}
      onKeyDown={onKeyDown}
      // Esc fires cancel; the parent decides whether the dialog stays mounted.
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
    >
      {children}
    </dialog>
  );
}

// The dialog's tab stops in order. A radio group is one stop: its checked radio.
function tabStops(dialog: HTMLElement): HTMLElement[] {
  return [...dialog.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
    (element) =>
      !(element instanceof HTMLInputElement) ||
      element.type !== 'radio' ||
      element.checked ||
      !dialog.querySelector(`input[type="radio"][name="${element.name}"]:checked`),
  );
}
