type TemplateLogoProps = {
  // Where this copy is mounted: at the end of the footer, or in the frame's
  // layers for slides without one.
  placement: 'footer' | 'frame';
};

// A look layer of the slide frame (T8): the template's logo. It draws from
// useSlideLook() alone and learns where it is only from the DOM,
// .sw-frame[data-layout] and .sw-frame[data-surface]; its own CSS hides the
// frame copy where a footer shows.
// TODO: draws nothing; the layer that reads the look replaces it.
// eslint-disable-next-line @typescript-eslint/no-unused-vars
export function TemplateLogo(_props: TemplateLogoProps) {
  return null;
}
