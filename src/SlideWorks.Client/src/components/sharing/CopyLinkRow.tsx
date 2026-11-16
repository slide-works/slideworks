import { useRef } from 'react';
import { useCopyText, type CopyState } from '../useCopyText';

const COPY_LABEL: Record<CopyState, string> = {
  idle: 'Copy link',
  copied: 'Copied',
  failed: 'Select and copy',
};

// The deck's link on the grey band, with a copy button. Without a clipboard the
// link is selected for the person to copy by hand.
export function CopyLinkRow({ link }: { link: string }) {
  const linkRef = useRef<HTMLElement>(null);
  const copy = useCopyText(linkRef);
  return (
    <div className="flex items-center gap-2 border-t border-edge bg-panel-2 px-[18px] py-2.5">
      <code
        ref={linkRef}
        data-testid="sharing-link"
        className="min-w-0 flex-1 truncate font-mono text-[12.5px] text-ink select-all"
      >
        {link}
      </code>
      <button
        type="button"
        data-testid="sharing-copy-link"
        className="btn-ghost bg-panel text-ink"
        onClick={() => copy.copy(link)}
      >
        {COPY_LABEL[copy.state]}
      </button>
      <span role="status" className="sr-only">
        {copy.state === 'copied' ? 'Link copied' : ''}
      </span>
    </div>
  );
}
