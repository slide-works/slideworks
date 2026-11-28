// The Reading in counter's test ids (SW-886 K11), drawn in the presenter window's top bar inside its
// presenter-reading-in wrapper. The acceptance page object Pages/PresenterPage.cs mirrors them.
export const READING_IN_TEST_IDS = {
  // The group: "Reading in" and one entry per language, named "Reading in: English 31, Thai 9, …"
  group: 'reading-in',
  // One entry per language drawn, such as "EN 31"
  count: 'reading-in-count',
  // "+2" for the languages past the fourth
  more: 'reading-in-more',
} as const;
