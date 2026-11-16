import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real image layout replaces it.
export function ImageLayout({ slots }: LayoutProps<'image'>) {
  return (
    <div data-testid="layout-image" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['image']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
