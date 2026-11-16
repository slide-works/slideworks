import { SLOT_KEYS, type LayoutProps } from '../slots';

// TODO: stub that lists the slot keys; the real title layout replaces it.
export function TitleLayout({ slots }: LayoutProps<'title'>) {
  return (
    <div data-testid="layout-title" className="flex flex-col gap-2 text-3xl">
      {SLOT_KEYS['title']
        .filter((key) => slots[key] !== undefined)
        .map((key) => (
          <span key={key} data-slot={key}>
            {key}
          </span>
        ))}
    </div>
  );
}
