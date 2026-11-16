import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real quote layout replaces it.
export function QuoteLayout({ slots }: LayoutProps<'quote'>) {
  return (
    <div data-testid="layout-quote" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['quote']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
