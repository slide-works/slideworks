import { useState } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { usePrinting } from '../printing';
import { useSlideShowing } from '../slideShowing';
import type { LayoutProps } from '../slots';
import {
  youtubeEmbedSrc,
  youtubePosterSrc,
  youtubeVideo,
  youtubeWatchHref,
  type YouTubeVideo,
} from '../youtubeVideo';
import styles from './VideoLayout.module.css';

// A headline over one YouTube video. A poster and a play button show until
// someone plays it, or until the slide is shown when play is 'auto'. The
// player is built from the video id, never from the link text. A link under
// it opens the same video on YouTube. While printing it loads nothing from
// YouTube: no poster and no player, only the frame, the play glyph and the link. A hidden slide drops the player, so a
// video that has started does not keep playing under the next slide.
export function VideoLayout({ slots, deck }: LayoutProps<'video'>) {
  const showing = useSlideShowing();
  const printing = usePrinting();
  const interactive = deck.interactive !== false;
  const [started, setStarted] = useState(false);
  const video = played(slots.url, slots.start);
  const playing =
    video !== undefined &&
    showing &&
    interactive &&
    !printing &&
    (started || slots.play === 'auto');
  const embed = video === undefined || !playing ? undefined : youtubeEmbedSrc(video, true);
  const poster = video === undefined ? undefined : youtubePosterSrc(video);
  const watch = video === undefined ? undefined : youtubeWatchHref(video);

  return (
    <div data-testid="layout-video" className="sw-layout">
      <SlideHeading
        layout="video"
        kicker={slots.kicker}
        headline={slots.headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div className={`sw-body ${styles.body}`}>
        <div className={styles.stage} data-testid="video-stage" data-video-player="">
          {video === undefined || poster === undefined ? (
            <p data-testid="video-missing" className={styles.missing}>
              This link is not a YouTube video.
            </p>
          ) : (
            <div className={styles.player}>
              {embed !== undefined ? (
                <iframe
                  data-testid="video-frame"
                  className={styles.frame}
                  src={embed}
                  title={slots.headline}
                  allow="autoplay; encrypted-media; picture-in-picture"
                  allowFullScreen
                  referrerPolicy="strict-origin-when-cross-origin"
                />
              ) : (
                <>
                  {printing ? null : (
                    <img data-testid="video-poster" className={styles.poster} alt="" src={poster} />
                  )}
                  {interactive && !printing ? (
                    <button
                      type="button"
                      data-testid="video-play"
                      className={styles.hit}
                      aria-label="Play the video"
                      onClick={(event) => {
                        event.stopPropagation();
                        setStarted(true);
                      }}
                    >
                      <PlayMark />
                    </button>
                  ) : (
                    <span className={styles.mark} aria-hidden="true">
                      <PlayMark />
                    </span>
                  )}
                </>
              )}
            </div>
          )}
        </div>
        <div className={styles.foot}>
          {slots.caption !== undefined ? (
            <p data-testid="video-caption" className={styles.caption}>
              {slots.caption}
            </p>
          ) : null}
          {watch !== undefined ? (
            <a
              data-testid="video-youtube"
              className={styles.link}
              href={watch}
              target="_blank"
              rel="noopener noreferrer"
              tabIndex={interactive ? undefined : -1}
              onClick={(event) => event.stopPropagation()}
            >
              Watch on YouTube
            </a>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function PlayMark() {
  return (
    <span className={styles.badge} aria-hidden="true">
      <svg className={styles.triangle} viewBox="0 0 28 32">
        <path d="M0 0 L28 16 L0 32 Z" fill="currentColor" />
      </svg>
    </span>
  );
}

// The video the slide plays. A start slot replaces the time in the link.
function played(url: string, start: number | undefined): YouTubeVideo | undefined {
  const parsed = youtubeVideo(url);
  if (parsed === undefined) return undefined;
  return start === undefined ? parsed : { id: parsed.id, start };
}
