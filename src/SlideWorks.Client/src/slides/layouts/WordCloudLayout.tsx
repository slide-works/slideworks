import type { CSSProperties } from 'react';
import { useOpenResponseScreen } from '../../openResponses/OpenResponseScreenLive';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// Canvas px: the word with the room's highest count is the largest, and a word nobody else sent is never smaller than
// SMALLEST_PX.
const SMALLEST_PX = 48;
const LARGEST_PX = 168;

const CLOUD: CSSProperties = {
  height: '100%',
  margin: 0,
  padding: 0,
  listStyle: 'none',
  display: 'flex',
  flexWrap: 'wrap',
  alignItems: 'center',
  alignContent: 'center',
  justifyContent: 'center',
  gap: '24px 56px',
};

// Grows with the word's count against the highest count on screen, so a word more guests sent is always larger.
function wordStyle(count: number, highest: number): CSSProperties {
  return {
    color: 'var(--sw-ink)',
    font: `800 ${Math.round(SMALLEST_PX + ((LARGEST_PX - SMALLEST_PX) * count) / highest)}px/1.1 var(--sw-font-head)`,
    whiteSpace: 'nowrap',
  };
}

// The word-cloud slide (light, SW-8): the question the room answers on their phones, and below it the round's words
// from OpenResponseScreenLive, hidden ones already left out, sized by their count. Anyone it gives no round to, such as
// a viewer who cannot present, sees the question only.
export function WordCloudLayout({ slots }: LayoutProps<'word-cloud'>) {
  const screen = useOpenResponseScreen();
  const words = screen?.type === 'wordCloud' ? screen.words : [];
  const highest = Math.max(1, ...words.map((word) => word.count));
  return (
    <div data-testid="layout-word-cloud" className="sw-layout">
      <SlideHeading layout="word-cloud" headline={slots.question} />
      <div data-testid="word-cloud-body" className="sw-body">
        {words.length > 0 && (
          <ul aria-label={`Words for: ${slots.question}`} style={CLOUD}>
            {words.map((word, index) => (
              <li
                key={word.text}
                data-testid={`word-cloud-word-${index}`}
                style={wordStyle(word.count, highest)}
              >
                {word.text}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
