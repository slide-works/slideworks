import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real numbered-cards layout replaces it.
export function NumberedCardsLayout({ slots }: LayoutProps<'numbered-cards'>) {
  return (
    <div data-testid="layout-numbered-cards" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['numbered-cards']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
