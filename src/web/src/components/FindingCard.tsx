import type { ReviewFinding } from "../types";
import { ConfidenceMeter, SeverityBadge } from "./Badges";

export function FindingCard({ finding }: { finding: ReviewFinding }) {
  return (
    <article className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <SeverityBadge severity={finding.severity} />
          <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600">
            {finding.category}
          </span>
        </div>
        <ConfidenceMeter value={finding.confidence} />
      </header>

      <h3 className="mt-3 text-sm font-semibold text-slate-900">{finding.title}</h3>

      <p className="mt-1 font-mono text-xs text-slate-500">
        {finding.file}
        {finding.line ? `:${finding.line}` : ""}
      </p>

      <p className="mt-2 text-sm leading-relaxed text-slate-700">{finding.description}</p>

      {finding.recommendedFix && (
        <div className="mt-3 rounded-md border border-emerald-200 bg-emerald-50 p-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
            Recommended fix
          </p>
          <p className="mt-1 text-sm text-emerald-900">{finding.recommendedFix}</p>
        </div>
      )}

      {finding.suggestedCode && (
        <pre className="mt-3 overflow-x-auto rounded-md bg-slate-900 p-3 text-xs text-slate-100">
          <code>{finding.suggestedCode}</code>
        </pre>
      )}
    </article>
  );
}
