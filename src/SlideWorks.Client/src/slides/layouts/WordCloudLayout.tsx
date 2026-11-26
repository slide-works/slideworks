import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
  type RefObject,
} from 'react';
import { useOpenResponseScreen } from '../../openResponses/OpenResponseScreenLive';
import type { CloudWord } from '../../openResponses/types';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { packWords, WORD_WEIGHT, type PackedWord, type Spot, type SpriteCache } from './packWords';

const NO_WORDS: readonly CloudWord[] = [];

const BOX: CSSProperties = { height: '100%', fontFamily: 'var(--sw-font-head)' };

type Size = { width: number; height: number };

// The body's size in layout px, which the player's scaling doesn't change; null until it has one.
function useBoxSize(ref: RefObject<HTMLElement | null>): Size | null {
  const [size, setSize] = useState<Size | null>(null);
  useLayoutEffect(() => {
    const element = ref.current;
    if (element === null) return;
    const observer = new ResizeObserver(() => {
      const width = element.offsetWidth;
      const height = element.offsetHeight;
      if (width === 0 || height === 0) return;
      setSize((was) => (was?.width === width && was.height === height ? was : { width, height }));
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref]);
  return size;
}

// Packs the words into the box once the heading font has loaded, so the masks are cut from the face the words are drawn
// in. Each pack starts every word from where the last one put it.
function usePackedWords(
  ref: RefObject<HTMLElement | null>,
  size: Size | null,
  words: readonly CloudWord[],
  turn: boolean,
): { packed: PackedWord[]; settled: boolean } {
  const [packed, setPacked] = useState<PackedWord[]>([]);
  const [placed, setPlaced] = useState<readonly CloudWord[] | null>(null);
  const spots = useRef(new Map<string, Spot>());
  const sprites = useRef<SpriteCache>(new Map());
  useEffect(() => {
    const element = ref.current;
    if (element === null || size === null || words.length === 0) return;
    let active = true;
    const font = getComputedStyle(element).fontFamily;
    void document.fonts
      .load(`${WORD_WEIGHT} 100px ${font}`)
      .catch(() => undefined)
      .then(() => {
        if (!active) return;
        const next = packWords(words, size, { font }, turn, spots.current, sprites.current);
        spots.current = new Map(next.map((word) => [word.text, { x: word.x, y: word.y }]));
        setPacked(next);
        setPlaced(words);
      });
    return () => {
      active = false;
    };
  }, [ref, size, words, turn]);
  return {
    packed: words.length === 0 ? [] : packed,
    settled: words.length === 0 || placed === words,
  };
}

function wordStyle(word: PackedWord): CSSProperties {
  return {
    fill: word.ink,
    fontFamily: 'var(--sw-font-head)',
    fontWeight: WORD_WEIGHT,
    fontSize: word.size,
  };
}

// The word-cloud slide (light, SW-8): the question the room answers on their phones, and below it the round's words
// from OpenResponseScreenLive, hidden ones already left out, sized by their count and packed inside the body (SW-606).
// Each word's ink comes from the palette and, with turn 'some', about one in three is turned on its side. Anyone it gives
// no round to, such as a viewer who cannot present, sees the question only.
export function WordCloudLayout({ slots }: LayoutProps<'word-cloud'>) {
  const screen = useOpenResponseScreen();
  const words = screen?.type === 'wordCloud' ? screen.words : NO_WORDS;
  const box = useRef<HTMLDivElement>(null);
  const size = useBoxSize(box);
  const { packed, settled } = usePackedWords(box, size, words, slots.turn === 'some');
  return (
    <div data-testid="layout-word-cloud" className="sw-layout">
      <SlideHeading layout="word-cloud" headline={slots.question} />
      <div
        data-testid="word-cloud-body"
        className="sw-body"
        data-packed={settled ? 'true' : undefined}
      >
        <div ref={box} style={BOX}>
          {words.length > 0 && size !== null && (
            <svg
              role="list"
              aria-label={`Words for: ${slots.question}`}
              width={size.width}
              height={size.height}
              viewBox={`${-size.width / 2} ${-size.height / 2} ${size.width} ${size.height}`}
              style={{ display: 'block' }}
            >
              {packed.map((word, index) => (
                <text
                  key={word.text}
                  role="listitem"
                  data-testid={`word-cloud-word-${index}`}
                  textAnchor="middle"
                  transform={`translate(${word.x} ${word.y}) rotate(${word.rotate})`}
                  style={wordStyle(word)}
                >
                  {word.text}
                </text>
              ))}
            </svg>
          )}
        </div>
      </div>
    </div>
  );
}
