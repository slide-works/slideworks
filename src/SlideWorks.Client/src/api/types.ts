// Mirrors the API's view models. Dates arrive pre-formatted (Created), never as
// ISO strings for the browser to parse — see .claude/skills/no-browser-datetime.
export type Note = {
  id: number;
  text: string;
  created: string;
};
