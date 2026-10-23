import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import type { LiveChatMessage } from "$lib/hubs/chatHub";

export interface LiveStreamerResponse {
  id: number;
  displayName: string;
}

export interface LiveStreamItem {
  id: number;
  roomName: string;
  title: string;
  taskTitle: string;
  taskId: number;
  streamer: LiveStreamerResponse;
  viewerCount: number;
  startedAt: string;
  visibility: number;
}

export interface LiveStreamPageDto {
  items: LiveStreamItem[];
  hasMore: boolean;
}

export interface JoinTokenResponse {
  url: string;
  token: string;
  roomName: string;
}

export interface LiveChatPage {
  messages: LiveChatMessage[];
  page: number;
  hasMore: boolean;
}

export async function fetchLiveStreams(
  limit = 20,
  exclude: string[] = [],
): Promise<LiveStreamPageDto> {
  const params = new URLSearchParams({ limit: String(limit) });
  exclude.forEach((name) => params.append("exclude", name));

  const response = await fetchWithCsrf(
    resolve(`/api/livestreams/active?${params}` as any),
    {
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch live streams");
  }

  return await response.json();
}

export async function joinLiveStream(
  roomName: string,
): Promise<JoinTokenResponse> {
  const response = await fetchWithCsrf(
    resolve(`/api/livestreams/join/${roomName}` as any),
    {
      method: "POST",
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to join live stream");
  }

  return await response.json();
}

export async function fetchLiveChat(
  roomName: string,
  page = 1,
  limit = 20,
): Promise<LiveChatPage> {
  const response = await fetchWithCsrf(
    resolve(
      `/api/livestreams/${roomName}/messages?page=${page}&limit=${limit}` as any,
    ),
    { credentials: "include" },
  );
  if (!response.ok) {
    throw new Error("Failed to fetch live chat");
  }
  return await response.json();
}
