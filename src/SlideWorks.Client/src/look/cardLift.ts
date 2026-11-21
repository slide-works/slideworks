import cardLiftJson from '@contracts/card-lift.json' with { type: 'json' };
import { relativeLuminance } from '../templates/editor/contrast';

// The card shadow a preview look carries (SW-148 D2): the rule of SlideWorks.Core's CardLift, with
// its values from src/contracts/card-lift.json, so a preview built on the client lifts cards as the
// server's look for the same template and palette does. The template's own shadow, except on paper
// whose relative luminance is under the threshold, where cards get the dark lift; a template whose
// shadow is none stays flat on any paper.
export const DARK_LIFT: string = cardLiftJson.dark;

export function cardLift(templateShadow: string, paper: string | undefined): string {
  if (templateShadow === cardLiftJson.none || paper === undefined) return templateShadow;
  const luminance = relativeLuminance(paper);
  return luminance !== null && luminance < cardLiftJson.darkPaperLuminance
    ? cardLiftJson.dark
    : templateShadow;
}
