type SlideFooterProps = {
  number: number;
  total: number;
  deckTitle: string;
  author: string | null;
};

// "3 / 16 · Ship smaller · Sam Rivera" along the bottom of a content slide.
export function SlideFooter({ number, total, deckTitle, author }: SlideFooterProps) {
  return (
    <p className="sw-footer" data-testid="slide-footer">
      <span>
        <b>{number}</b> / {total} · {deckTitle}
        {author === null ? null : ` · ${author}`}
      </span>
    </p>
  );
}
