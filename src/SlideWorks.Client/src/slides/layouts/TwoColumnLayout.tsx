import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real two-column layout replaces it.
export function TwoColumnLayout({ slots }: LayoutProps<'two-column'>) {
  return (
    <div data-testid="layout-two-column" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['two-column']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
