import * as signalR from "@microsoft/signalr";
import { resolve } from "$app/paths";
import type { Message } from "$lib/api/directMessages";

export type ChatMessage = Message & { receiverId: number };

export type LiveChatMessage = {
  id: number;
  roomName: string;
  senderId: number;
  senderName: string;
  content: string;
  sentAt: string;
};

const HUB_PATH = "/api/hubs/chat";

let connection: signalR.HubConnection | undefined;
let starting: Promise<void> | undefined;
const listeners = new Set<(message: ChatMessage) => void>();
const readListeners = new Set<
  (readerId: number, lastReadMessageId: number) => void
>();

const liveStreamListeners = new Set<(message: LiveChatMessage) => void>();

function getConnection() {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(resolve(HUB_PATH as any), { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on("ReceiveMessage", (message: ChatMessage) => {
      listeners.forEach((listener) => listener(message));
    });

    connection.on("ReceiveLiveMessage", (message: LiveChatMessage) => {
      liveStreamListeners.forEach((listener) => listener(message));
    });

    connection.on(
      "ConversationRead",
      (readerId: number, lastReadMessageId: number) => {
        readListeners.forEach((listener) =>
          listener(readerId, lastReadMessageId),
        );
      },
    );
  }
  return connection;
}

export function startChat(): Promise<void> {
  const c = getConnection();
  if (c.state !== signalR.HubConnectionState.Disconnected) {
    return starting ?? Promise.resolve();
  }
  starting = c.start().finally(() => {
    starting = undefined;
  });
  return starting;
}

export async function stopChat() {
  await connection?.stop();
}

export async function sendChatMessage(receiverId: number, content: string) {
  await startChat();
  await getConnection().invoke("SendMessage", receiverId, content);
}

export function onChatMessage(listener: (message: ChatMessage) => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function onConversationRead(
  listener: (readerId: number, lastReadMessageId: number) => void,
) {
  readListeners.add(listener);
  return () => {
    readListeners.delete(listener);
  };
}

export async function markConversationRead(otherUserId: number) {
  await getConnection().invoke("MarkConversationRead", otherUserId);
}

export async function joinLiveChat(roomName: string) {
  await startChat();
  await getConnection().invoke("JoinStream", roomName);
}

export async function leaveLiveChat(roomName: string) {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected)
    return;
  await connection.invoke("LeaveStream", roomName);
}

export async function sendLiveChatMessage(roomName: string, content: string) {
  await startChat();
  await getConnection().invoke("SendLiveMessage", roomName, content);
}

export function onLiveChatMessage(
  listener: (message: LiveChatMessage) => void,
) {
  liveStreamListeners.add(listener);
  return () => liveStreamListeners.delete(listener);
}
