import type { ReactNode } from 'react';

// Speaker notes (P3) as React nodes. The notes are plain text with newlines, and their only markup is "**bold**" and
// lines that start with "- " (a list). They are never HTML: everything else, angle brackets included, is shown as typed.
export function NotesText({ text }: { text: string }) {
  const blocks: ReactNode[] = [];
  let items: string[] = [];
  const endList = () => {
    if (items.length === 0) return;
    blocks.push(
      <ul key={blocks.length} className="list-disc pl-5">
        {items.map((item, at) => (
          <li key={at}>{inline(item)}</li>
        ))}
      </ul>,
    );
    items = [];
  };
  for (const line of text.split('\n')) {
    if (line.startsWith('- ')) {
      items.push(line.slice(2));
      continue;
    }
    endList();
    if (line.trim() !== '') blocks.push(<p key={blocks.length}>{inline(line)}</p>);
  }
  endList();
  return <>{blocks}</>;
}

// "**bold**" → <b>. A "**" with no partner is kept as typed.
function inline(line: string): ReactNode[] {
  const parts = line.split('**');
  if (parts.length % 2 === 0) {
    // An odd number of markers: the last one has no partner, so join it back on.
    const tail = parts.splice(-2).join('**');
    parts.push(tail);
  }
  return parts
    .map((part, at) => (at % 2 === 1 ? <b key={at}>{part}</b> : part))
    .filter((part) => part !== '');
}
