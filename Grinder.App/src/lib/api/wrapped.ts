import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";

export interface WrappedEligibilityResponse {
  isEligible: boolean;
  // Most recent year the user can view a Wrapped for (drives the always-available profile button).
  mostRecentEligibleYear: number | null;
  // Current year's Wrapped, only set during its December window (drives the auto popup).
  currentEligibleYear: number | null;
  dismissed: boolean;
}

export interface WrappedConstantEligibilityResponse {
  currentYearEligible: boolean;
  currentYear: number;
  previousYearEligible: boolean;
  previousYear: number;
}

export interface WrappedMostLikedTask {
  id: number;
  title: string;
  reactionCount: number;
}

export interface WrappedMostLikedComment {
  id: number;
  taskId: number;
  content: string;
  replyCount: number;
}

export interface WrappedCategoryStat {
  name: string;
  taskCount: number;
}

export interface WrappedExtraStats {
  badgesEarnedCount: number;
  numberOfFriendsMade: number;
  numberOfNewFollowers: number;
  numberOfMessagesSent: number;
  numberOfSharedTasks: number;
  numberOfCommentsWritten: number;
  numberOfReactionsReceived: number;
  numberOfTasksOverdue: number;
  mostProductiveMonth: WrappedProductiveMonth | null;
}

export interface WrappedProductiveMonth {
  month: string;
  taskCount: number;
}

export interface WrappedStatsResponse {
  tasksStarted: number;
  tasksCompleted: number;
  completionPercent: number;
  mostLikedTask: WrappedMostLikedTask | null;
  mostLikedComment: WrappedMostLikedComment | null;
  topCategories: WrappedCategoryStat[];
  extraStats: WrappedExtraStats | null;
}

const EMPTY_STATUS: WrappedEligibilityResponse = {
  isEligible: false,
  mostRecentEligibleYear: null,
  currentEligibleYear: null,
  dismissed: false,
};

export async function fetchWrappedStatus(): Promise<WrappedEligibilityResponse> {
  const res = await fetch(resolve("/api/wrapped/eligibility" as any), {
    method: "GET",
    credentials: "include",
  });

  if (!res.ok) {
    return EMPTY_STATUS;
  }

  const data: WrappedEligibilityResponse = await res.json();
  return {
    isEligible: data.isEligible,
    mostRecentEligibleYear: data.mostRecentEligibleYear ?? null,
    currentEligibleYear: data.currentEligibleYear ?? null,
    dismissed: data.dismissed ?? false,
  };
}

export async function checkWrappedEligibility(): Promise<boolean> {
  const { isEligible } = await fetchWrappedStatus();
  return isEligible;
}

export async function dismissWrapped(year: number): Promise<void> {
  await fetchWithCsrf(resolve("/api/wrapped/dismiss" as any), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify({ year }),
  });
}

export async function fetchWrappedStats(
  year: number,
): Promise<WrappedStatsResponse | null> {
  const res = await fetch(resolve(`/api/wrapped/stats?year=${year}` as any), {
    method: "GET",
    credentials: "include",
  });

  if (!res.ok) {
    return null;
  }

  return res.json();
}

export async function fetchWrappedConstantEligibility(): Promise<WrappedConstantEligibilityResponse | null> {
  const res = await fetch(resolve("/api/wrapped/constant-eligibility" as any), {
    method: "GET",
    credentials: "include",
  });

  if (!res.ok) {
    return null;
  }

  return res.json();
}
