import type { Severity } from "../types";

const STYLES: Record<Severity, string> = {
  Critical: "bg-red-100 text-red-800 ring-red-300",
  High: "bg-orange-100 text-orange-800 ring-orange-300",
  Medium: "bg-amber-100 text-amber-800 ring-amber-300",
  Low: "bg-sky-100 text-sky-800 ring-sky-300",
  Info: "bg-slate-100 text-slate-700 ring-slate-300",
};

export function SeverityBadge({ severity }: { severity: Severity }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset ${STYLES[severity]}`}
    >
      {severity}
    </span>
  );
}

export function ConfidenceMeter({ value }: { value: number }) {
  const tone =
    value >= 75 ? "bg-emerald-500" : value >= 50 ? "bg-amber-500" : "bg-slate-400";

  return (
    <div className="flex items-center gap-2" title={`Confidence ${value}%`}>
      <div className="h-1.5 w-20 overflow-hidden rounded-full bg-slate-200">
        <div className={`h-full ${tone}`} style={{ width: `${Math.min(value, 100)}%` }} />
      </div>
      <span className="text-xs font-medium text-slate-600">{value}%</span>
    </div>
  );
}
