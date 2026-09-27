import { useMemo, useState } from "react";
import { requestReview } from "./api";
import { FindingCard } from "./components/FindingCard";
import { ReviewSummary } from "./components/ReviewSummary";
import { SEVERITY_ORDER, type ReviewResult, type Severity } from "./types";

export default function App() {
  const [url, setUrl] = useState("");
  const [result, setResult] = useState<ReviewResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [severityFilter, setSeverityFilter] = useState<Severity | "All">("All");

  const grouped = useMemo(() => {
    if (!result) return [];
    return SEVERITY_ORDER.map((severity) => ({
      severity,
      findings: result.findings.filter((f) => f.severity === severity),
    })).filter((group) => group.findings.length > 0);
  }, [result]);

  const visible = useMemo(
    () =>
      severityFilter === "All"
        ? grouped
        : grouped.filter((g) => g.severity === severityFilter),
    [grouped, severityFilter],
  );

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setResult(null);
    setSeverityFilter("All");

    try {
      setResult(await requestReview(url.trim()));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unexpected error.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto max-w-5xl px-6 py-6">
          <h1 className="text-2xl font-bold text-slate-900">AI Code Review Agent</h1>
          <p className="mt-1 text-sm text-slate-500">
            Paste a GitHub pull request URL to get quality, security, testing and
            maintainability findings.
          </p>
        </div>
      </header>

      <main className="mx-auto max-w-5xl space-y-6 px-6 py-8">
        <form onSubmit={onSubmit} className="flex flex-col gap-3 sm:flex-row">
          <input
            type="text"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://github.com/owner/repo/pull/123"
            className="flex-1 rounded-md border border-slate-300 bg-white px-4 py-2.5 text-sm shadow-sm outline-none focus:border-slate-500"
            required
          />
          <button
            type="submit"
            disabled={loading || url.trim().length === 0}
            className="rounded-md bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-slate-400"
          >
            {loading ? "Reviewing…" : "Review pull request"}
          </button>
        </form>

        {error && (
          <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">
            {error}
          </div>
        )}

        {loading && (
          <div className="rounded-md border border-slate-200 bg-white p-6 text-sm text-slate-500">
            Fetching the diff and analysing the changes…
          </div>
        )}

        {result && (
          <>
            <ReviewSummary result={result} />

            <div className="flex flex-wrap items-center gap-2">
              <FilterButton
                label={`All (${result.findings.length})`}
                active={severityFilter === "All"}
                onClick={() => setSeverityFilter("All")}
              />
              {SEVERITY_ORDER.map((severity) => {
                const count = result.severityCounts?.[severity] ?? 0;
                if (count === 0) return null;
                return (
                  <FilterButton
                    key={severity}
                    label={`${severity} (${count})`}
                    active={severityFilter === severity}
                    onClick={() => setSeverityFilter(severity)}
                  />
                );
              })}
            </div>

            {result.findings.length === 0 ? (
              <div className="rounded-md border border-emerald-200 bg-emerald-50 p-6 text-sm text-emerald-800">
                No issues were found in this pull request.
              </div>
            ) : (
              visible.map((group) => (
                <section key={group.severity} className="space-y-3">
                  <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
                    {group.severity} · {group.findings.length}
                  </h2>
                  <div className="space-y-3">
                    {group.findings.map((finding) => (
                      <FindingCard key={finding.id} finding={finding} />
                    ))}
                  </div>
                </section>
              ))
            )}
          </>
        )}
      </main>
    </div>
  );
}

function FilterButton({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-full px-3 py-1 text-xs font-semibold ring-1 ring-inset ${
        active
          ? "bg-slate-900 text-white ring-slate-900"
          : "bg-white text-slate-600 ring-slate-300 hover:bg-slate-100"
      }`}
    >
      {label}
    </button>
  );
}
