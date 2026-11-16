import { useState } from 'react';

type CopyState = 'idle' | 'copied' | 'failed';

const COPY_LABEL: Record<CopyState, string> = {
  idle: 'Copy',
  copied: 'Copied',
  failed: 'Select and copy',
};

// "Connect an agent": the MCP server URL with a copy button. The library shows
// it until the presenter's first agent connects (connect.show).
export function ConnectBanner({ mcpUrl }: { mcpUrl: string }) {
  const [copy, setCopy] = useState<CopyState>('idle');

  const copyUrl = () =>
    navigator.clipboard.writeText(mcpUrl).then(
      () => setCopy('copied'),
      () => setCopy('failed'),
    );

  return (
    <div
      data-testid="connect-banner"
      className="flex flex-wrap items-center gap-3 rounded-panel border border-accent/25 bg-accent-soft px-4 py-3 text-meta text-ink"
    >
      <svg
        aria-hidden
        width="18"
        height="18"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        className="shrink-0 text-accent-dark"
      >
        <path d="M9 17H7a5 5 0 0 1 0-10h2M15 7h2a5 5 0 0 1 0 10h-2M8 12h8" />
      </svg>
      <span>
        <b className="text-accent-dark">Connect an agent:</b> add this MCP server, then ask it to
        &ldquo;make a deck about …&rdquo;
      </span>
      <code
        data-testid="connect-mcp-url"
        className="select-all rounded-md border border-edge bg-panel px-2 py-1 font-mono text-[12.5px]"
      >
        {mcpUrl}
      </code>
      <button
        type="button"
        data-testid="connect-copy"
        className="rounded-md border border-edge-strong bg-panel px-3 py-1.5 text-meta font-medium text-ink"
        onClick={copyUrl}
      >
        {COPY_LABEL[copy]}
      </button>
    </div>
  );
}
