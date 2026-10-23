import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";

export interface Badge {
  id: number;
  name: string;
  icon: string;
  description: string;
  isUnlocked: boolean;
  earnedAt: string | null;
  progress: number;
  threshold: number;
}

export async function fetchBadges(
  fetch: typeof globalThis.fetch = globalThis.fetch,
): Promise<Response> {
  return fetch(resolve(`/api/badge` as any), {
    credentials: "include",
  });
}

export async function selectBadge(badgeId: number | null): Promise<Response> {
  return fetchWithCsrf(resolve(`/api/badge/select` as any), {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify({ badgeId }),
  });
}
