import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real cards layout replaces it.
export function CardsLayout({ slots }: LayoutProps<'cards'>) {
  return (
    <div data-testid="layout-cards" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['cards']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
