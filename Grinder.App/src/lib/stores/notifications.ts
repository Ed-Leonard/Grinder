import { writable } from "svelte/store";
import type { NotificationDto } from "$lib/api/notifications";
import { unreadWeight } from "$lib/utils/notificationDisplay";

/*
 * Holds the notifications state for current user globally across app.
 * Handles notifications list, and unread count
 * Filters to prevent duplicate entries which can be caused by spamming follow etc.
 */
function createNotificationStore() {
  const { subscribe, update } = writable({
    items: [] as NotificationDto[],
    unreadCount: 0,
  });
  return {
    subscribe,
    setInitial: (items: NotificationDto[], unreadCount: number) =>
      update((s) => ({ ...s, items, unreadCount })),
    prepend: (n: NotificationDto) =>
      update((s) => ({
        ...s,
        items: [n, ...s.items.filter((existing) => existing.id !== n.id)],
      })),
    setUnreadCount: (count: number) =>
      update((s) => ({ ...s, unreadCount: count })),
    markReadLocal: (id: number) =>
      update((s) => {
        const target = s.items.find((n) => n.id === id);
        const weight = target && !target.read ? unreadWeight(target) : 0;
        return {
          ...s,
          items: s.items.map((n) => (n.id === id ? { ...n, read: true } : n)),
          unreadCount: Math.max(0, s.unreadCount - weight),
        };
      }),
    markAllReadLocal: () =>
      update((s) => ({
        ...s,
        items: s.items.map((n) => ({ ...n, read: true })),
        unreadCount: 0,
      })),
  };
}

export const notificationStore = createNotificationStore();
