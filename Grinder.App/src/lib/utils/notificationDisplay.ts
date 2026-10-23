import type { NotificationDto, NotificationType } from "$lib/api/notifications";

const TYPE_BY_INDEX: NotificationType[] = [
  "Follow",
  "Comment",
  "React",
  "Message",
];

export function iconFor(n: NotificationDto): NotificationType {
  return typeof n.type === "number" ? TYPE_BY_INDEX[n.type]! : n.type;
}

export function isFollowNotification(n: NotificationDto): boolean {
  return iconFor(n) === "Follow";
}

// Message notifications are bumped rather than duplicated, so one unread row can represent several messages.
export function unreadWeight(n: NotificationDto): number {
  return iconFor(n) === "Message" ? n.messageCount : 1;
}

export function messageFor(n: NotificationDto): string {
  switch (iconFor(n)) {
    case "Follow":
      return `${n.senderName} started following you`;
    case "Comment":
      return `${n.senderName} commented: "${n.commentContent}"`;
    case "React":
      return `${n.senderName} reacted to your task:`;
    case "Message":
      return `${n.senderName} sent you a message: "${n.messagePreview}"`;
    default:
      return `${n.senderName} sent a notification`;
  }
}

export function initialFor(name: string): string {
  const trimmed = name.trim();
  return trimmed ? trimmed[0]!.toUpperCase() : "?";
}

export function timeAgo(dateString: string): string {
  const seconds = Math.floor(
    (Date.now() - new Date(dateString).getTime()) / 1000,
  );
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}
