import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import { RelationshipStatus } from "$lib/types";

interface RelationshipStatusesResponse {
  relationships: Record<string, RelationshipStatus>;
}

export interface Friend {
  id: number;
  displayName: string;
  profilePictureUrl: string | null;
  relationshipStatus: RelationshipStatus;
}

export async function getRelationshipStatuses(
  userIds: number[],
): Promise<Record<number, RelationshipStatus>> {
  const params = userIds
    .map((id) => `userIds=${encodeURIComponent(String(id))}`)
    .join("&");

  const response = await fetch(
    resolve(
      `/api/follows/relationship-statuses${params ? `?${params}` : ""}` as any,
    ),
    {
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch relationship statuses");
  }

  const data = (await response.json()) as RelationshipStatusesResponse;

  return Object.fromEntries(
    Object.entries(data.relationships).map(([userId, status]) => [
      Number(userId),
      status as RelationshipStatus,
    ]),
  ) as Record<number, RelationshipStatus>;
}

export async function followUser(userId: number): Promise<void> {
  const response = await fetchWithCsrf(
    resolve(`/api/follows/${userId}` as any),
    {
      method: "POST",
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to follow user");
  }
}

export async function unfollowUser(userId: number): Promise<void> {
  const response = await fetchWithCsrf(
    resolve(`/api/follows/${userId}` as any),
    {
      method: "DELETE",
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to unfollow user");
  }
}

export async function getFriends(
  userId: number,
  fetch: typeof globalThis.fetch = globalThis.fetch,
): Promise<Friend[]> {
  const response = await fetch(
    resolve(`/api/follows/friends/${userId}` as any),
    {
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch friends");
  }

  return await response.json();
}
