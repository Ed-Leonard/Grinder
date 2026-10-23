import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import { type Friend } from "./follows";
import { type TaskAttachment } from "$lib/types";

export interface ConversationPreview {
  otherUserId: number;
  otherUserDisplayName: string;
  lastMessagePreview: string;
  lastMessageSenderId: number;
  lastMessageSentAt: string;
  unreadCount: number;
  hasUnread: boolean;
}

export interface Message {
  id: number;
  senderId: number;
  content: string | null;
  sharedTaskId: number | null;
  sharedTask: SharedTaskPreview | null;
  read: boolean;
  sentAt: string;
}

export interface SharedTaskPreview {
  id: number;
  userId: number;
  userDisplayName: string;
  title: string;
  isClip: boolean;
  videoAttachment: TaskAttachment | null;
  imageAttachment: TaskAttachment | null;
}

export interface ConversationPage {
  messages: Message[];
  page: number;
  hasMore: boolean;
}

export async function fetchConversationPreviews(): Promise<
  ConversationPreview[]
> {
  const response = await fetchWithCsrf(
    resolve(`/api/directmessages/conversations` as any),
    { credentials: "include" },
  );

  if (!response.ok) {
    throw new Error("Failed to load conversations");
  }
  return response.json();
}

export async function fetchConversation(
  otherUserId: number,
  page: number = 1,
  limit: number = 20,
): Promise<ConversationPage> {
  const response = await fetchWithCsrf(
    resolve(
      `/api/directmessages/conversation/${otherUserId}?page=${page}&limit=${limit}` as any,
    ),
    { credentials: "include" },
  );

  if (!response.ok) {
    throw new Error("Failed to load conversations");
  }
  return response.json();
}

export async function getShareableFriendsForTask(
  taskId: number,
): Promise<Friend[]> {
  const response = await fetchWithCsrf(
    resolve(`/api/directmessages/${taskId}/shareable-friends` as any),
    { credentials: "include" } as any,
  );

  if (!response.ok) {
    throw new Error("Failed to load shareable friends");
  }
  return response.json();
}

export async function shareTask(taskId: number, receiverIds: number[]) {
  const response = await fetchWithCsrf(
    resolve(`/api/directmessages/share-task` as any),
    {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ taskId, receiverIds }),
    },
  );

  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(error?.error ?? "Failed to share task");
  }
}
