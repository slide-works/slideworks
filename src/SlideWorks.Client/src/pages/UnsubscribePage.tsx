// /unsubscribe, where a share email's link leads, open without a sign-in. TODO: read t from the fragment and add the
// one button that posts it (POST /api/email/unsubscribe); for now it only says where it is.
export function UnsubscribePage() {
  return (
    <main data-testid="unsubscribe-page" className="mx-auto grid max-w-md gap-3 px-4 py-16">
      <h1 className="text-lg font-bold tracking-tight text-ink">Unsubscribe</h1>
    </main>
  );
}
