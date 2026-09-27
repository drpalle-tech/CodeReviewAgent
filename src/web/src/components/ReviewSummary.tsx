import type { ReviewResult } from "../types";
import { ConfidenceMeter } from "./Badges";

const RECOMMENDATION_STYLES: Record<string, string> = {
  approve: "bg-emerald-100 text-emerald-800 ring-emerald-300",
  comment: "bg-sky-100 text-sky-800 ring-sky-300",
  request_changes: "bg-red-100 text-red-800 ring-red-300",
};

export function ReviewSummary({ result }: { result: ReviewResult }) {
  const pr = result.pullRequest;
  const style = RECOMMENDATION_STYLES[result.recommendation] ?? RECOMMENDATION_STYLES.comment;

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">{pr.title}</h2>
          <p className="mt-1 text-sm text-slate-500">
            {pr.reference.slug} · {pr.author} · {pr.headBranch} → {pr.baseBranch} ·{" "}
            <span className="text-emerald-600">+{pr.additions}</span>{" "}
            <span className="text-red-600">-{pr.deletions}</span> across {pr.changedFileCount} files
          </p>
        </div>
        <span
          className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ring-1 ring-inset ${style}`}
        >
          {result.recommendation.replace("_", " ")}
        </span>
      </div>

      <p className="mt-4 text-sm leading-relaxed text-slate-700">{result.summary}</p>

      <div className="mt-4 flex flex-wrap items-center gap-6">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
            Overall confidence
          </p>
          <div className="mt-1">
            <ConfidenceMeter value={result.overallConfidence} />
          </div>
        </div>
        <p className="text-xs text-slate-500">
          Engine: <span className="font-mono">{result.engine}</span>
        </p>
      </div>

      {result.categoryScores.length > 0 && (
        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {result.categoryScores.map((score) => (
            <div key={score.category} className="rounded-md border border-slate-200 p-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-slate-700">{score.category}</span>
                <span className="text-sm font-semibold text-slate-900">{score.score}</span>
              </div>
              <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-200">
                <div
                  className={`h-full ${score.score >= 80 ? "bg-emerald-500" : score.score >= 55 ? "bg-amber-500" : "bg-red-500"}`}
                  style={{ width: `${score.score}%` }}
                />
              </div>
              <p className="mt-2 text-xs text-slate-500">{score.summary}</p>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
