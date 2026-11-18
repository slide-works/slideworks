import { useSlideLook } from '../lookContext';
import './topbar.css';

// A look layer of the slide frame (T8): the template's accent bar across the top
// (D13). It draws from useSlideLook() alone; topbar.css decides on which slides
// it shows, from the frame's own attributes.
export function TopBar() {
  const { topBar } = useSlideLook();
  if (topBar !== 'accent') return null;
  return <i className="sw-top-bar" aria-hidden="true" data-testid="top-bar" />;
}
