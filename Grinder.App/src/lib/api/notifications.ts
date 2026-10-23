import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import type { Reaction } from "$lib/api/reaction";

export type NotificationType = "Follow" | "Comment" | "React" | "Message";

export interface NotificationDto {
  id: number;
  senderId: number;
  senderName: string;
  type: NotificationType;
  entityId: number | null;
  read: boolean;
  createdAt: string;
  commentContent: string | null;
  reaction: Reaction | null;
  messageCount: number;
  messagePreview: string | null;
}

export interface NotificationPageDto {
  items: NotificationDto[];
  page: number;
  totalPages: number;
}

export interface FetchNotificationsOptions {
  page?: number;
  limit?: number;
  type?: NotificationType | null;
  unreadOnly?: boolean;
}

export async function fetchNotifications(
  options: FetchNotificationsOptions = {},
): Promise<NotificationPageDto> {
  const { page = 1, limit = 20, type = null, unreadOnly = false } = options;

  const params = new URLSearchParams({
    page: String(page),
    limit: String(limit),
    unreadOnly: String(unreadOnly),
  });
  if (type) params.set("type", type);

  const response = await fetchWithCsrf(
    resolve(`/api/notifications?${params}` as any),
    {
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch notifications");
  }

  return response.json();
}

export async function fetchUnreadCount(): Promise<number> {
  const response = await fetchWithCsrf(
    resolve(`/api/notifications/unread-count` as any),
    { credentials: "include" },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch unread count");
  }

  const data = await response.json();
  return data.count;
}

export async function markAsRead(id: number): Promise<void> {
  const response = await fetchWithCsrf(
    resolve(`/api/notifications/${id}/read` as any),
    { method: "PATCH", credentials: "include" },
  );

  if (!response.ok) {
    throw new Error("Failed to mark notification as read");
  }
}

export async function markAllAsRead(): Promise<void> {
  const response = await fetchWithCsrf(
    resolve(`/api/notifications/read-all` as any),
    { method: "PATCH", credentials: "include" },
  );

  if (!response.ok) {
    throw new Error("Failed to mark all notifications as read");
  }
}

export async function dismissNotification(id: number): Promise<void> {
  const response = await fetchWithCsrf(
    resolve(`/api/notifications/${id}` as any),
    { method: "DELETE", credentials: "include" },
  );

  if (!response.ok) {
    throw new Error("Failed to dismiss notification");
  }
}
