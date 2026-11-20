import { useState } from 'react';
import { useSlideLook } from '../lookContext';
import './logo.css';

type TemplateLogoProps = {
  // Where this copy is mounted: at the end of the footer, or in the frame's
  // layers for slides without one.
  placement: 'footer' | 'frame';
};

// One decorative image. A file that fails to load hides itself, so a slide
// never shows a broken-image mark.
function LogoImage({ url, className, testId }: { url: string; className: string; testId: string }) {
  const [failed, setFailed] = useState(false);
  if (failed) return null;
  return (
    <img
      className={className}
      data-testid={testId}
      src={url}
      alt=""
      onError={() => setFailed(true)}
    />
  );
}

// A look layer of the slide frame (T8): the template's logo pair (D12). It
// draws from useSlideLook() alone and learns where it is only from the DOM,
// .sw-frame[data-layout] and .sw-frame[data-surface]; logo.css decides on
// which slides each image shows. In the footer the light logo shows on a light
// slide and the dark one on a dark slide, only when the deck has no logo of its
// own. In the frame the dark logo sits in the corner of title, divider and
// close slides.
export function TemplateLogo({ placement }: TemplateLogoProps) {
  const { logo, logos } = useSlideLook();
  if (placement === 'frame') {
    if (!logos.showOnDark || logos.dark === null) return null;
    return (
      <LogoImage
        key={logos.dark}
        url={logos.dark}
        className="sw-tlogo sw-tlogo-corner"
        testId="corner-logo"
      />
    );
  }
  if (logo !== null) return null;
  return (
    <>
      {logos.light === null ? null : (
        <LogoImage
          key={logos.light}
          url={logos.light}
          className="sw-tlogo sw-tlogo-light"
          testId="template-logo-light"
        />
      )}
      {logos.dark === null ? null : (
        <LogoImage
          key={logos.dark}
          url={logos.dark}
          className="sw-tlogo sw-tlogo-dark"
          testId="template-logo-dark"
        />
      )}
    </>
  );
}
