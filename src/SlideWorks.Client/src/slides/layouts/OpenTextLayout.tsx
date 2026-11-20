import { useOpenResponseScreen } from '../../openResponses/OpenResponseScreenLive';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import styles from './OpenTextLayout.module.css';

// The open-text slide (light, SW-8): the question, and the room's answers in number order. The answers come from the
// live screen (hidden ones already left out), never from the slots. With no live round the body stays empty.
export function OpenTextLayout({ slots }: LayoutProps<'open-text'>) {
  const screen = useOpenResponseScreen();
  const answers =
    screen?.type === 'openText'
      ? [...screen.answers].sort((left, right) => left.number - right.number)
      : [];

  return (
    <div data-testid="layout-open-text" className="sw-layout">
      <SlideHeading layout="open-text" headline={slots.question} />
      <div data-testid="open-text-body" className={`sw-body ${styles.body}`}>
        {answers.length > 0 && (
          <ol data-testid="open-text-wall" className={styles.wall} aria-label="Answers">
            {answers.map((answer) => (
              <li key={answer.number} data-testid="open-text-answer" className={styles.answer}>
                <span data-testid="open-text-number" className={styles.number}>
                  {answer.number}
                </span>
                <p data-testid="open-text-text" className={styles.text} dir="auto">
                  {answer.text}
                </p>
              </li>
            ))}
          </ol>
        )}
      </div>
    </div>
  );
}
