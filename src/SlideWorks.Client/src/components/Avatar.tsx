import type { AvatarView } from '../api/types';

// Each colour key of C2 as a soft background with a dark text of the same hue.
const COLOURS: Record<string, { background: string; color: string }> = {
  slate: { background: '#e2e8f0', color: '#334155' },
  red: { background: '#fde2e0', color: '#9b2c22' },
  amber: { background: '#fdecc8', color: '#8a5a06' },
  green: { background: '#d9f0e2', color: '#17653f' },
  teal: { background: '#d3eef0', color: '#0f6068' },
  blue: { background: '#dde6fc', color: '#1e3a8a' },
  violet: { background: '#e8e0fa', color: '#5b3a9e' },
  pink: { background: '#fbe0ee', color: '#9d2a64' },
};

const SIZES = { sm: 'h-7 w-7 text-[11px]', lg: 'h-16 w-16 text-xl' } as const;

// A person as C2 describes them: their picture when there is one, else their initials on their colour. The picture is
// decoration (the name is shown beside it), so it has no alt text; testId names the whole avatar.
export function Avatar({
  avatar,
  size = 'sm',
  testId = 'avatar',
}: {
  avatar: AvatarView;
  size?: keyof typeof SIZES;
  testId?: string;
}) {
  const colours = COLOURS[avatar.colour] ?? COLOURS.slate;
  return (
    <span
      data-testid={testId}
      data-colour={avatar.colour}
      style={avatar.pictureUrl ? undefined : colours}
      className={`inline-grid shrink-0 place-items-center overflow-hidden rounded-full bg-panel-2 font-semibold ${SIZES[size]}`}
    >
      {avatar.pictureUrl ? (
        <img
          src={avatar.pictureUrl}
          alt=""
          data-testid={`${testId}-picture`}
          className="h-full w-full object-cover"
        />
      ) : (
        <span data-testid={`${testId}-initials`}>{avatar.initials}</span>
      )}
    </span>
  );
}
