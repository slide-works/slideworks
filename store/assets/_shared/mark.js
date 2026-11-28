// The SlideWorks app mark: a slide with a second slide peeking out behind it and a "live" dot.
// Drawn on a 1024 canvas; every shape sits inside the 626 px safe circle Android's adaptive icons keep.
window.SW = window.SW || {};
window.SW.mark = function mark(variant, size) {
  const front = { x: 262, y: 380, w: 500, h: 312, r: 44 };
  const back = { x: 312, y: 332, w: 400, h: 312, r: 44 };
  const bars = [
    { x: 312, y: 440, w: 220, h: 36 },
    { x: 312, y: 512, w: 380, h: 22 },
    { x: 312, y: 560, w: 300, h: 22 },
  ];
  const dot = { cx: 700, cy: 636, r: 24 };
  const gradient = '<linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#3d6bf5"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient>';
  const glow = '<radialGradient id="n" cx="0.85" cy="1.1" r="0.9"><stop offset="0" stop-color="#2f5bea" stop-opacity="0.55"/><stop offset="1" stop-color="#2f5bea" stop-opacity="0"/></radialGradient>';
  const rect = (o, attrs) => `<rect x="${o.x}" y="${o.y}" width="${o.w}" height="${o.h}" rx="${o.r ?? o.h / 2}" ${attrs}/>`;
  const barRects = (fill) => bars.map((b) => rect(b, `fill="${fill[bars.indexOf(b)]}"`)).join('');
  const circle = (fill) => `<circle cx="${dot.cx}" cy="${dot.cy}" r="${dot.r}" fill="${fill}"/>`;
  // iOS icons have no safe-zone circle, so the mark can be bigger there.
  const grow = (shapes) => `<g transform="translate(512 512) scale(1.35) translate(-512 -512)">${shapes}</g>`;
  let defs = '', body = '', viewBox = '0 0 1024 1024';
  if (variant === 'light') {
    defs = gradient;
    body = `<rect width="1024" height="1024" fill="url(#g)"/>${grow(`${rect(back, 'fill="#fff" fill-opacity="0.45"')}${rect(front, 'fill="#fff"')}${barRects(['#2f5bea', '#cbd5f5', '#cbd5f5'])}${circle('#fcd34d')}`)}`;
  } else if (variant === 'dark') {
    defs = glow;
    body = `<rect width="1024" height="1024" fill="#0b1020"/><rect width="1024" height="1024" fill="url(#n)"/>${grow(`${rect(back, 'fill="#2f5bea" fill-opacity="0.7"')}${rect(front, 'fill="#141b33" stroke="#5b7cff" stroke-width="8"')}${barRects(['#a5b8ff', '#4a5578', '#4a5578'])}${circle('#fcd34d')}`)}`;
  } else if (variant === 'tinted') {
    body = `<rect width="1024" height="1024" fill="#000"/>${grow(`${rect(back, 'fill="#fff" fill-opacity="0.4"')}${rect(front, 'fill="#fff"')}${barRects(['#333', '#aaa', '#aaa'])}${circle('#666')}`)}`;
  } else if (variant === 'foreground') {
    body = `${rect(back, 'fill="#fff" fill-opacity="0.45"')}${rect(front, 'fill="#fff"')}${barRects(['#2f5bea', '#cbd5f5', '#cbd5f5'])}${circle('#fcd34d')}`;
  } else if (variant === 'background') {
    defs = gradient;
    body = '<rect width="1024" height="1024" fill="url(#g)"/>';
  } else if (variant === 'monochrome') {
    const cut = (inner) => inner;
    defs = `<mask id="m" maskUnits="userSpaceOnUse" x="0" y="0" width="1024" height="1024"><rect width="1024" height="1024" fill="#fff"/>${rect({ ...front, x: front.x - 16, y: front.y - 16, w: front.w + 32, h: front.h + 32, r: front.r + 16 }, 'fill="#000"')}</mask>` +
      `<mask id="f" maskUnits="userSpaceOnUse" x="0" y="0" width="1024" height="1024"><rect width="1024" height="1024" fill="#fff"/>${barRects(['#000', '#000', '#000'])}<circle cx="${dot.cx}" cy="${dot.cy}" r="${dot.r}" fill="#000"/></mask>`;
    body = cut(`<g mask="url(#m)">${rect(back, 'fill="#fff"')}</g><g mask="url(#f)">${rect(front, 'fill="#fff"')}</g>`);
  } else if (variant === 'splash-light' || variant === 'splash-dark') {
    const dark = variant === 'splash-dark';
    viewBox = '202 252 620 520';
    body = `${rect(back, `fill="${dark ? '#5b7cff' : '#2f5bea'}" fill-opacity="${dark ? 0.7 : 0.35}"`)}${rect(front, `fill="${dark ? '#fff' : '#2f5bea'}"`)}${barRects(dark ? ['#2f5bea', '#cbd5f5', '#cbd5f5'] : ['#fff', '#b9c8f8', '#b9c8f8'])}${circle('#fcd34d')}`;
  } else throw new Error('unknown mark variant ' + variant);
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}" width="${size}" height="${size}"><defs>${defs}</defs>${body}</svg>`;
};
