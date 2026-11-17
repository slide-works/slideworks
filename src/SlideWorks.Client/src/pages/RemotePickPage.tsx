// /remote, where a phone picks which of the person's live talks to drive (P8): signed in, with no Shell chrome.
// The QR code's link (/remote?deck={id}) lands here too.
export function RemotePickPage() {
  // TODO: placeholder until the pick page is built: the live list (P4), its cards and empty state, and the move from
  // ?deck={id} to /remote/{id} replace it.
  return (
    <main data-testid="remote-pick" className="mx-auto max-w-md px-4 py-16">
      <h1 className="page-title">Pick a talk</h1>
    </main>
  );
}
