import { useState } from 'react';
import { WAVE_PATHS } from '../../art/waves';
import { useSlideLook } from '../lookContext';
import './art.css';

// An uploaded image. A file that fails to load hides itself, so a title slide
// never shows a broken-image mark.
function ArtImage({ url }: { url: string }) {
  const [failed, setFailed] = useState(false);
  if (failed) return null;
  return (
    <img
      className="sw-title-art"
      data-testid="title-art"
      src={url}
      alt=""
      aria-hidden="true"
      onError={() => setFailed(true)}
    />
  );
}

// A look layer of the slide frame (T8): the template's art behind the title
// slide. It draws from useSlideLook() alone; art.css shows it only under
// .sw-frame[data-layout='title']. A generator it does not know draws nothing.
export function TitleArt() {
  const { art } = useSlideLook();
  if (art.kind === 'generator' && art.generator === 'waves') {
    return (
      <svg
        className="sw-title-art"
        data-testid="title-art"
        viewBox="0 0 1920 1080"
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        {WAVE_PATHS.map((path) => (
          <path key={path.d} d={path.d} style={{ opacity: path.opacity }} />
        ))}
      </svg>
    );
  }
  if (art.kind === 'image' && art.url !== null) return <ArtImage key={art.url} url={art.url} />;
  return null;
}
