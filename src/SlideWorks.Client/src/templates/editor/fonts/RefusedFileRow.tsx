export type FileProblem = {
  key: number;
  fileName: string;
  pill: string;
  message: string;
};

// One file of a pick that was not stored: its name, a pill, and the server's sentence in an alert.
export function RefusedFileRow({ problem }: { problem: FileProblem }) {
  return (
    <li className="grid gap-1 rounded-panel border border-bad bg-bad-soft p-2 text-meta">
      <div className="flex items-center gap-2">
        <span className="font-medium text-ink">{problem.fileName}</span>
        <span className="inline-flex rounded-full bg-bad px-2 py-0.5 text-xs font-medium whitespace-nowrap text-white">
          {problem.pill}
        </span>
      </div>
      <p role="alert" className="text-ink">
        {problem.message}
      </p>
    </li>
  );
}
