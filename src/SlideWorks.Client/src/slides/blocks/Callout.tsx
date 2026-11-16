import type { CalloutBlock } from '../slots';

// TODO: stub; the real callout block (tone colours) replaces it.
export function Callout({ tone, text }: CalloutBlock) {
  return (
    <div data-testid="block-callout" data-tone={tone}>
      {text}
    </div>
  );
}
