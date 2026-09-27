import type { ReviewResult } from "./types";

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

interface ProblemDetails {
  title?: string;
  detail?: string;
}

export async function requestReview(
  pullRequestUrl: string,
  signal?: AbortSignal,
): Promise<ReviewResult> {
  const response = await fetch(`${BASE_URL}/api/reviews`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ pullRequestUrl }),
    signal,
  });

  if (!response.ok) {
    let message = `Review failed with status ${response.status}.`;
    try {
      const problem = (await response.json()) as ProblemDetails;
      message = problem.detail ?? problem.title ?? message;
    } catch {
      // Response body was not problem details; keep the default message.
    }
    throw new Error(message);
  }

  return (await response.json()) as ReviewResult;
}
