import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import { TaskStatus } from "$lib/types";
import type { ReactionResponse } from "./reaction";

export interface FeedAttachment {
  id: number;
  contentType: string;
  originalFileName: string;
}

export interface FeedCategory {
  id: number;
  name: string;
}

export interface FeedItem {
  id: number;
  userId: number;
  userDisplayName: string;
  selectedBadgeName: string | null;
  selectedBadgeIcon: string | null;
  title: string;
  description: string | null;
  createdAt: string;
  isUpdated: boolean;
  changedFields: number[] | null;
  startDate: string | null;
  startTime: string | null;
  dueDate: string;
  dueTime: string;
  itemStatus: TaskStatus;
  taskAttachments: FeedAttachment[];
  categories: FeedCategory[];
  reactions: ReactionResponse[];
}

export interface FeedPageDto {
  items: FeedItem[];
  page: number;
  totalPages: number;
}

export async function fetchFeed(
  page: number = 1,
  limit: number = 20,
): Promise<FeedPageDto> {
  const params = new URLSearchParams({
    page: String(page),
    limit: String(limit),
  });

  const response = await fetchWithCsrf(resolve(`/api/feed?${params}` as any), {
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error("Failed to fetch feed");
  }

  return await response.json();
}

export async function markTasksViewed(taskItemIds: number[]): Promise<void> {
  if (taskItemIds.length === 0) return;

  await fetch(resolve("/api/feed/viewed" as any), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify({ taskItemIds }),
  });
}
