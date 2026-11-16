export type LogoFieldProps = {
  deckId: string;
  logo: { url: string } | null;
  onChange(imageId: string | null): void;
};

// TODO: a shell with inert controls; the logo field (upload through
// POST /api/decks/{id}/images, preview, remove) replaces it.
export function LogoField({ logo }: LogoFieldProps) {
  return (
    <div data-testid="logo-field" className="grid gap-1 text-meta">
      <span>Logo</span>
      {logo ? <img data-testid="logo-field-image" src={logo.url} alt="Deck logo" /> : null}
      <input data-testid="logo-field-file" type="file" accept="image/*" disabled />
      <button data-testid="logo-field-remove" type="button" disabled>
        Remove logo
      </button>
    </div>
  );
}
