import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import { type ReactionResponse } from "$lib/api/reaction";

export interface ClipAttachment {
  id: number;
  contentType: string;
}

export interface ClipItem {
  id: number;
  userId: number;
  userDisplayName: string;
  userProfilePictureUrl: string | null;
  title: string;
  description: string | null;
  createdAt: string;
  taskAttachments: ClipAttachment[];
  selectedBadgeName: string | null;
  selectedBadgeIcon: string | null;
  reactions: ReactionResponse[];
}

export interface ClipPageDto {
  items: ClipItem[];
  page: number;
  totalPages: number;
}

export async function fetchClips(
  page: number = 1,
  limit: number = 20,
): Promise<ClipPageDto> {
  const params = new URLSearchParams({
    page: String(page),
    limit: String(limit),
  });

  const response = await fetchWithCsrf(resolve(`/api/clip?${params}` as any), {
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error("Failed to fetch clips");
  }

  return await response.json();
}
