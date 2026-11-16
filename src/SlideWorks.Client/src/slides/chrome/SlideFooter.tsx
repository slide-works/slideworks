type SlideFooterProps = {
  number: number;
  total: number;
  deckTitle: string;
  // The author, or the server's date label in a template whose footer shows
  // the date; null leaves the last item out.
  meta: string | null;
  // The deck's logo, root-relative (/api/images/{id}), or null.
  logoUrl: string | null;
  // The deck is a draft: a chip before the logo.
  draft: boolean;
};

// "3 / 16 · Ship smaller · Sam Rivera" along the bottom of a content slide,
// with the draft chip and the deck's logo on the right (mockup section 04).
// The row is one 17 px text line; the chip and logo are centred on it and
// overflow it, so the text line stays at y = 1023 whatever they hold.
export function SlideFooter({ number, total, deckTitle, meta, logoUrl, draft }: SlideFooterProps) {
  return (
    <div className="sw-footer" data-testid="slide-footer">
      <span data-testid="slide-footer-text">
        <b>{number}</b> / {total} · {deckTitle}
        {meta === null ? null : ` · ${meta}`}
      </span>
      {draft || logoUrl !== null ? (
        <span className="sw-footer-end">
          {draft ? (
            <span className="sw-draft-chip" data-testid="draft-chip">
              Draft
            </span>
          ) : null}
          {logoUrl !== null ? (
            <img className="sw-logo" data-testid="slide-logo" src={logoUrl} alt="" />
          ) : null}
        </span>
      ) : null}
    </div>
  );
}
