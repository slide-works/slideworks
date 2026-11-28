// The five store screenshots, in caption order. A set's asset.html loads this and sets the sizes in CSS.
const SHOTS = [
  ['1-home', 'home', false, 'Join a talk with a code', 'Type the six digits from the screen'],
  ['2-join', 'join', false, 'Follow the slide live', 'See the slide number the speaker is on'],
  ['3-talks', 'talks', false, 'Open any of your talks', 'Sign in and open any of your decks'],
  ['4-talk', 'talk', true, 'Your deck, live on your phone', 'The current slide, updated as it changes'],
  ['5-sign-in', 'signin', false, 'Sign in through your browser', 'The hosted server, or your own'],
];
document.body.innerHTML = SHOTS.map(([id, screen, dark, title, sub]) =>
  `<section class="shot" data-shot="${id}"><div class="head"><h2>${title}</h2><p>${sub}</p></div>${SW.phone(screen, dark)}</section>`).join('');
