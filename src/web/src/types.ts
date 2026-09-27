export type Severity = "Info" | "Low" | "Medium" | "High" | "Critical";

export type ReviewCategory =
  | "Quality"
  | "Security"
  | "Testing"
  | "Maintainability"
  | "Performance";

export interface ChangedFile {
  path: string;
  status: string;
  additions: number;
  deletions: number;
  patch: string | null;
}

export interface PullRequestContext {
  reference: { owner: string; repo: string; number: number; slug: string };
  title: string;
  body: string | null;
  author: string;
  baseBranch: string;
  headBranch: string;
  state: string;
  additions: number;
  deletions: number;
  changedFileCount: number;
  files: ChangedFile[];
}

export interface ReviewFinding {
  id: string;
  file: string;
  line: number | null;
  category: ReviewCategory;
  severity: Severity;
  title: string;
  description: string;
  recommendedFix: string | null;
  suggestedCode: string | null;
  confidence: number;
}

export interface CategoryScore {
  category: ReviewCategory;
  score: number;
  summary: string;
}

export interface ReviewResult {
  reviewId: string;
  pullRequestUrl: string;
  pullRequest: PullRequestContext;
  summary: string;
  overallConfidence: number;
  recommendation: string;
  categoryScores: CategoryScore[];
  findings: ReviewFinding[];
  engine: string;
  createdAt: string;
  severityCounts: Record<string, number>;
}

export const SEVERITY_ORDER: Severity[] = [
  "Critical",
  "High",
  "Medium",
  "Low",
  "Info",
];
