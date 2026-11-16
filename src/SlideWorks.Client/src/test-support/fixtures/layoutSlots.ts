import type { Slots } from '../../slides/slots';

// The id the sample image is served under in tests (stubFetch, stubApi).
export const SAMPLE_IMAGE_ID = 'img_sample0001';

// One valid slide's slots for every layout, with every optional slot set.
export const LAYOUT_SLOTS: { [L in keyof Slots]: Slots[L] } = {
  title: {
    kicker: 'Team offsite · 2026',
    title: 'Ship smaller',
    subtitle: 'Why small batches beat big plans, and how to start on Monday',
    tagline: 'One change a day beats one launch a quarter.',
    author: 'Sam Rivera',
    affiliation: 'Product Engineering',
    stamp: 'Draft',
  },
  divider: {
    title: 'Big batches hide the risk',
    sub: 'Why the quarter-long launch keeps slipping.',
  },
  bullets: {
    kicker: 'The cost',
    headline: 'Every slip we had last year started as a big batch',
    items: ['Risk shows up late.', { lead: 'Merges get scary.', text: 'Long branches collide.' }],
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Nobody can say no to a slice.' },
  },
  'two-column': {
    kicker: 'Slice it',
    headline: 'A story map shows the thinnest release',
    left: { title: 'How to read it', items: ['One row is one release.'] },
    right: { image: { image: SAMPLE_IMAGE_ID, alt: 'A story map' } },
  },
  practice: {
    kicker: 'Slice it',
    headline: 'Slice by what a customer can do, not by layer',
    do: [{ lead: 'Start from one user action', text: 'and build every layer it needs, thinly.' }],
    comes_back: ['Feedback in days, not quarters.'],
    avoid: ['"API first, UI later."'],
    callout: { tone: 'bad', text: "If a slice can't be demoed, it isn't a slice yet." },
  },
  compare: {
    kicker: 'Two teams, one quarter',
    headline: 'The difference is when the customer gets to answer',
    failure: {
      label: 'Checkout',
      title: 'Sliced by layer',
      sub: 'One launch',
      points: ['Nothing to show for 11 weeks.'],
    },
    success: {
      label: 'Search',
      title: 'Sliced by customer action',
      sub: 'Nine releases',
      points: ['Something live every week.'],
    },
  },
  'big-number': {
    kicker: 'The cost',
    headline: 'It took us eleven weeks to get a change in front of a customer',
    value: '11 wks',
    caption: 'Median from first commit to the first customer using it',
    detail: 'Eight of our 12 epics.',
    source: 'Source: delivery dashboard, Jan–Dec',
  },
  quote: {
    kicker: 'From the retro',
    quote: "We stopped asking 'is it done?' and started asking 'is it out?'",
    attribution: 'Search team retro, Q3',
  },
  image: {
    kicker: 'Slice it',
    headline: 'The story map',
    image: { image: SAMPLE_IMAGE_ID, alt: 'A story map' },
    points: ['One row is one release.'],
    caption: 'The first row shipped in a week.',
  },
  table: {
    kicker: 'Ship it',
    headline: 'Aim for slices a pair can ship inside a week',
    columns: ['Slice', 'Size', 'Status'],
    rows: [
      ['Save a search', 'S', { text: 'Shipped', tone: 'good' }],
      ['Share a search', 'M', { text: 'Late', tone: 'warn' }],
    ],
    highlightRow: 1,
  },
  chart: {
    kicker: 'Ship it',
    headline: 'Lead time fell as the slices got smaller',
    kind: 'bar',
    xLabels: ['Jul', 'Aug', 'Sep', 'Oct'],
    series: [{ name: 'Lead time (days)', values: [24, 18, 9, 6] }],
    yUnit: 'days',
    caption: 'Median lead time per month.',
    marker: { x: 2.5, label: 'Flags on' },
  },
  gantt: {
    kicker: 'The cost',
    headline: 'One milestone each looks fair',
    columns: ['S1', 'S2', 'S3', 'S4'],
    rows: [
      { label: 'Checkout API', start: 1, end: 2, status: 'done' },
      { label: 'Checkout UI', start: 2, end: 4, status: 'slips', note: 'Waited on the API' },
    ],
    caption: 'Everything is green until it isn’t.',
  },
  'numbered-cards': {
    kicker: 'Make it stick',
    headline: 'Four questions, in this order',
    cards: [
      { title: 'Who notices?', text: 'Name the customer.' },
      { title: 'What can they do?', text: 'One sentence.' },
    ],
    reveal: 'one-by-one',
  },
  rules: {
    kicker: 'Make it stick',
    headline: 'Three rules for the first month',
    rules: [{ title: 'Merge to main every day', text: 'Behind a flag if it isn’t ready.' }],
    reveal: 'all',
  },
  cards: {
    kicker: 'Make it stick',
    headline: 'Everyone changes one habit',
    cards: [
      {
        badge: 'PM',
        title: 'Write the demo sentence',
        text: 'Before the ticket.',
        chips: ['Planning'],
      },
    ],
    reveal: 'all',
  },
  close: {
    headline: 'Pick one story.\nShip half of it by Friday.',
    sub: 'Then tell us what you learned at the next demo.',
    author: 'Sam Rivera · Product Engineering',
  },
};
