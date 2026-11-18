import { useRef } from 'react';

// What the server accepts for a logo or title art; the server checks the file, this only filters the picker.
const ACCEPT = '.svg,.png,.webp,image/svg+xml,image/png,image/webp';

type Props = {
  id: string;
  label: string;
  busy: boolean;
  describedBy: string | undefined;
  onPick(file: File): void;
};

// A button that opens the file picker for one image. It does not disable itself while an upload
// runs (focus would be lost): a click while busy does nothing.
export function ImagePicker({ id, label, busy, describedBy, onPick }: Props) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <button
        id={id}
        type="button"
        className="btn-ghost"
        aria-describedby={describedBy}
        onClick={() => {
          if (!busy) input.current?.click();
        }}
      >
        {label}
      </button>
      <input
        ref={input}
        type="file"
        accept={ACCEPT}
        tabIndex={-1}
        aria-hidden="true"
        className="sr-only"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = '';
          if (file) onPick(file);
        }}
      />
    </>
  );
}
