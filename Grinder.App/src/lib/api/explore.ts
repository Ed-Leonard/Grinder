import { type TaskAttachment } from "$lib/types";
import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";

export interface ExploreItem {
  id: number;
  taskAttachments: TaskAttachment[];
}

export interface ExplorePageDto {
  items: ExploreItem[];
  page: number;
  totalPages: number;
}

export async function fetchExploreFeed(
  page: number = 1,
  limit: number = 20,
): Promise<ExplorePageDto> {
  const params = new URLSearchParams({
    page: String(page),
    limit: String(limit),
  });

  const response = await fetchWithCsrf(
    resolve(`/api/explore?${params}` as any),
    {
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch explore feed");
  }

  return await response.json();
}
